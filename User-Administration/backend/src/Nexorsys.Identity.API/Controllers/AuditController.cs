using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Core.Abstractions;
using System.Security.Claims;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class AuditController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public AuditController(AppDbContext dbContext, IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetAuditLogs()
		{
			// Fetch logs, ordered by newest first, limit to 500.
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var entitlement = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
			if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			var logs = await _dbContext.Set<AuditLog>().Where(a => a.OrganizationId == _organizationContext.OrganizationId.Value)
				.Include(a => a.User)
				.OrderByDescending(a => a.CreatedAt)
				.Take(500)
				.Select(a => new
				{
					a.Id,
					a.Action,
					a.ResourceType,
					a.ResourceId,
					OldValues = AuditRedactor.Redact(a.OldValues),
					NewValues = AuditRedactor.Redact(a.NewValues),
					a.IpAddress,
					a.UserAgent,
					a.CorrelationId,
					a.CreatedAt,
					User = a.User != null && a.User.OrganizationId == _organizationContext.OrganizationId.Value ? new
					{
						a.User.DisplayName,
						a.User.SamAccountName
					} : null
				})
				.ToListAsync();

			// Resolve target names in memory to avoid expression tree limitations with switch
			var enrichedLogs = logs.Select(a => new
			{
				a.Id,
				a.Action,
				a.ResourceType,
				a.OldValues,
				a.NewValues,
				a.IpAddress,
				a.UserAgent,
				a.CorrelationId,
				a.CreatedAt,
				a.User,
				TargetName = a.ResourceType switch
				{
					"Utilisateur" or "User" => _dbContext.Set<User>().Where(u => u.Id == a.ResourceId && u.OrganizationId == _organizationContext.OrganizationId!.Value).Select(u => u.DisplayName).FirstOrDefault(),
					"UserPin" or "MIE" or "Equipment" => _dbContext.Set<UserPin>().Where(up => up.Id == a.ResourceId && up.OrganizationId == _organizationContext.OrganizationId!.Value && up.User != null && up.User.OrganizationId == _organizationContext.OrganizationId.Value).Include(up => up.User).Select(up => up.User != null ? up.User.DisplayName : null).FirstOrDefault(),
					"Workflow" => _dbContext.Set<Workflow>().Where(w => w.Id == a.ResourceId && w.OrganizationId == _organizationContext.OrganizationId!.Value && w.User != null && w.User.OrganizationId == _organizationContext.OrganizationId.Value).Include(w => w.User).Select(w => w.User != null ? w.User.DisplayName : null).FirstOrDefault(),
					_ => null
				}
			});

			return Ok(enrichedLogs);
		}

	}
}
