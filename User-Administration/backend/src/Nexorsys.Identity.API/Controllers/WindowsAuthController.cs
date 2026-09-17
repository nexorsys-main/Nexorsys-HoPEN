using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/windows-auth")]
	[Authorize]
	public class WindowsAuthController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organization;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public WindowsAuthController(AppDbContext dbContext, IOrganizationContext organization,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organization = organization;
			_licenseGuard = licenseGuard;
		}

		[HttpPost("enroll")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Enroll([FromBody] EnrollRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (string.IsNullOrEmpty(request.DeviceCertificateThumbprint) || string.IsNullOrEmpty(request.UserId))
			{
				return Unauthorized(new { message = "Device Certificate Thumbprint and UserId are required for Machine Identity validation." });
			}

			var workstation = await _dbContext.Workstations.FirstOrDefaultAsync(w => w.OrganizationId == _organization.OrganizationId.GetValueOrDefault() &&
				w.IsActive && w.EnrollmentState == WorkstationLifecycle.Active &&
				w.DeviceCertificateThumbprint == request.DeviceCertificateThumbprint && w.Hostname == request.WorkstationHostname);
			if (workstation == null) return NotFound("Workstation not authorized.");

			if (!Guid.TryParse(request.UserId, out var userGuid)) return BadRequest("Invalid UserId.");
			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userGuid &&
				u.OrganizationId == _organization.OrganizationId.GetValueOrDefault() && u.IsActive &&
				(!u.LockedUntil.HasValue || u.LockedUntil <= DateTime.UtcNow));
			if (user == null) return NotFound("User not found.");

			// Update user enrollment status
			user.EnrollmentStatus = "Enrolled";
			user.EnrollmentWorkstation = workstation.Hostname;
			user.EnrollmentExpirationDate = DateTime.UtcNow.AddMonths(3); // 3 month expiration
			user.LastCredentialRefresh = DateTime.UtcNow;
			user.RequiresReEnrollment = false;
			_dbContext.AuthenticationEvents.Add(new AuthenticationEvent
			{
				Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, UserId = user.Id, WorkstationId = workstation.Id,
				Timestamp = DateTime.UtcNow, ProviderType = "Windows", EventType = "CredentialEnrolled",
				Result = "Success", Reason = "Administrative enrollment completed"
			});

			await _dbContext.SaveChangesAsync();

			return Ok(new { message = "Enrollment processed", workstation = request.WorkstationHostname, expiration = user.EnrollmentExpirationDate });
		}

		[HttpPost("revoke")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> Revoke([FromBody] RevokeRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!Guid.TryParse(request.UserId, out var userGuid)) return BadRequest("Invalid UserId.");
			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userGuid && u.OrganizationId == _organization.OrganizationId.GetValueOrDefault());
			if (user == null) return NotFound("User not found.");

			user.EnrollmentStatus = "Revoked";
			user.RequiresReEnrollment = true;
			user.EnrollmentWorkstation = null;

			// Log event
			_dbContext.AuthenticationEvents.Add(new AuthenticationEvent
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organization.OrganizationId.GetValueOrDefault(),
				Timestamp = DateTime.UtcNow,
				UserId = user.Id,
				ProviderType = "Windows",
				EventType = "CredentialRevoked",
				Result = "Success",
				Reason = SanitizeEventReason(request.Reason, "Manual revocation")
			});

			await _dbContext.SaveChangesAsync();
			return Ok(new { message = "Credentials revoked." });
		}

		[HttpPost("reenroll")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> ReEnroll([FromBody] ReEnrollRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!Guid.TryParse(request.UserId, out var userGuid)) return BadRequest("Invalid UserId.");
			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userGuid && u.OrganizationId == _organization.OrganizationId.GetValueOrDefault());
			if (user == null) return NotFound("User not found.");

			user.RequiresReEnrollment = true;
			user.EnrollmentStatus = "PendingReEnrollment";

			_dbContext.AuthenticationEvents.Add(new AuthenticationEvent
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organization.OrganizationId.GetValueOrDefault(),
				Timestamp = DateTime.UtcNow,
				UserId = user.Id,
				ProviderType = "Windows",
				EventType = "ReEnrollmentTriggered",
				Result = "Success",
				Reason = "Badge replaced or expired"
			});

			await _dbContext.SaveChangesAsync();
			return Ok(new { message = "User flagged for re-enrollment at next login." });
		}
		[HttpPost("session-created")]
		public IActionResult SessionCreated([FromBody] SessionCreatedRequest request)
		{
			// Hostname and badge UID are identifiers, not proof of a Windows logon.
			return StatusCode(StatusCodes.Status501NotImplemented, new
			{
				message = "Cryptographically verified Windows logon proof is not configured. No session was created."
			});
		}

		private static string SanitizeEventReason(string? reason, string fallback)
		{
			var value = string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
			value = value[..Math.Min(value.Length, 256)];
			return AuditRedactor.Redact(value) ?? fallback;
		}

		private async Task<IActionResult?> RequireIdentityEntitlementAsync()
		{
			if (!_organization.OrganizationId.HasValue) return Forbid();
			var decision = await _licenseGuard.HasFeatureAsync(_organization.OrganizationId.Value, "identity");
			return decision.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = decision.Code });
		}
	}

	// DTOs
	public class EnrollRequest
	{
		public string? WorkstationHostname { get; set; }
		public string? DeviceCertificateThumbprint { get; set; }
		public string? UserId { get; set; }
	}
	public class RevokeRequest { public string? UserId { get; set; } public string? Reason { get; set; } }
	public class ReEnrollRequest { public string? UserId { get; set; } }

	public class SessionCreatedRequest
	{
		public string? WorkstationHostname { get; set; }
		public string? BadgeUid { get; set; }
		public string? UserId { get; set; }
	}

}
