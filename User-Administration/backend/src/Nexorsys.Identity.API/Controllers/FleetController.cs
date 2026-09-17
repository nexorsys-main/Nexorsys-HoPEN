using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Linq;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Nexorsys.Identity.API.Filters;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/fleet")]
	[ApiKeyAuth]
	public class FleetController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public FleetController(AppDbContext dbContext, IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet("workstations")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> GetFleetWorkstations()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "kiosk");
			if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			var workstations = await _dbContext.Workstations
				.Where(w => w.OrganizationId == organizationId)
				.Include(w => w.CredentialProviderFleet)
				.Select(w => new
				{
					id = w.Id,
					hostname = w.Hostname,
					department = w.Department,
					version = w.CredentialProviderFleet != null && w.CredentialProviderFleet.OrganizationId == organizationId
						? w.CredentialProviderFleet.InstalledVersion : "Inconnu",
					machineSid = w.MachineSid,
					certificateThumbprint = w.DeviceCertificateThumbprint,
					isTrusted = w.IsActive,
					lastSeen = w.LastSeenAt ?? w.CreatedAt
				})
				.ToListAsync();

			return Ok(workstations);
		}

		[HttpPut("workstations/{id}/trust")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> ToggleWorkstationTrust(Guid id, [FromBody] ToggleTrustRequest request)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			if (request.IsTrusted)
			{
				var entitlement = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
				if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			}
			var workstation = await _dbContext.Workstations.FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == _organizationContext.OrganizationId.Value);
			if (workstation == null) return NotFound("Workstation not found");
			if (request.IsTrusted && workstation.EnrollmentState != WorkstationLifecycle.Active)
				return Conflict("Only an actively enrolled workstation can be trusted; complete certificate approval and proof first.");

			var wasTrusted = workstation.IsActive;
			workstation.IsActive = request.IsTrusted;
			var actorId = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(_dbContext, User, workstation.OrganizationId);
			_dbContext.AuditLogs.Add(new AuditLog
			{
				Id = Guid.NewGuid(), OrganizationId = workstation.OrganizationId,
				Action = "Workstation trust changed", ResourceType = "Workstation", ResourceId = workstation.Id,
				OldValues = $"IsActive={wasTrusted}; EnrollmentState={workstation.EnrollmentState}",
				NewValues = $"IsActive={workstation.IsActive}; EnrollmentState={workstation.EnrollmentState}",
				CreatedAt = DateTime.UtcNow,
				UserId = actorId
			});
			await _dbContext.SaveChangesAsync();

			return Ok(new { success = true, isTrusted = workstation.IsActive });
		}
		
		[HttpPost("heartbeat")]
		[AllowAnonymous]
		public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
		{
			if (!_organizationContext.OrganizationId.HasValue)
				return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "kiosk");
			if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			if (!HttpContext.Items.TryGetValue("AuthenticatedWorkstationId", out var trustedWorkstation) || trustedWorkstation is not Guid workstationId)
				return Unauthorized("An authenticated workstation certificate is required.");
			var foreignFleetBindingExists = await _dbContext.CredentialProviderFleets.IgnoreQueryFilters()
				.AnyAsync(f => f.WorkstationId == workstationId && f.OrganizationId != organizationId);
			if (foreignFleetBindingExists)
				return Conflict("Fleet ownership does not match the authenticated workstation.");

			var workstation = await _dbContext.Workstations
				.Include(w => w.CredentialProviderFleet)
			.FirstOrDefaultAsync(w => w.Id == workstationId && w.OrganizationId == organizationId);

			if (workstation == null)
				return Unauthorized("Workstation is not enrolled for this organization.");
			if (!workstation.IsActive) return Forbid();
			if (workstation.CredentialProviderFleet is { } existingFleet && existingFleet.OrganizationId != workstation.OrganizationId)
				return Conflict("Fleet ownership does not match the authenticated workstation.");
			workstation.LastSeenAt = DateTime.UtcNow;

			if (workstation.CredentialProviderFleet == null)
			{
				workstation.CredentialProviderFleet = new CredentialProviderFleet
				{
					Id = Guid.NewGuid(),
					OrganizationId = workstation.OrganizationId,
					WorkstationId = workstation.Id,
					InstalledVersion = request.AppVersion,
					LastCheckInAt = DateTime.UtcNow,
					ProviderStatus = "Online"
				};
				_dbContext.CredentialProviderFleets.Add(workstation.CredentialProviderFleet);
			}
			else
			{
				workstation.CredentialProviderFleet.InstalledVersion = request.AppVersion;
				workstation.CredentialProviderFleet.LastCheckInAt = DateTime.UtcNow;
				workstation.CredentialProviderFleet.ProviderStatus = "Online";
			}

			await _dbContext.SaveChangesAsync();

			// No server-side fleet configuration catalog/distribution adapter exists yet.
			// Never fabricate a hash or echo the client value as proof of configuration state.
			return Ok(new { latestConfigHash = (string?)null });
		}
	}

	public class ToggleTrustRequest
	{
		public bool IsTrusted { get; set; }
	}

	public class HeartbeatRequest
	{
		public string? Hostname { get; set; }
		public string? AppVersion { get; set; }
		public string? ConfigHash { get; set; }
		public string? ApiKey { get; set; }
	}
}
