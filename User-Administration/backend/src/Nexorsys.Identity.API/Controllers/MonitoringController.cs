using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/monitoring")]
	[Authorize(Policy = "AdminOnly")]
	public class MonitoringController : ControllerBase
	{
		private const int MaximumRecentEventLimit = 200;
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public MonitoringController(AppDbContext dbContext, IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet("stats")]
		public async Task<IActionResult> GetStats()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity");
			if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			var now = DateTime.UtcNow;

			var totalWorkstations = await _dbContext.Workstations.Where(w => w.OrganizationId == organizationId).CountAsync();
			var offlineWorkstations = await _dbContext.CredentialProviderFleets
				.Where(f => f.OrganizationId == organizationId && (f.ProviderStatus != "Online" || f.LastCheckInAt < now.AddMinutes(-15)))
				.CountAsync();

			var today = now.Date;
			var successfulLoginsToday = await _dbContext.AuthenticationEvents
				.Where(e => e.OrganizationId == organizationId && e.Timestamp >= today && e.Result == "Success")
				.CountAsync();

			var failedLoginsToday = await _dbContext.AuthenticationEvents
				.Where(e => e.OrganizationId == organizationId && e.Timestamp >= today && e.Result == "Failure")
				.CountAsync();

			var expiringEnrollments = await _dbContext.Users
				.Where(u => u.OrganizationId == organizationId && u.EnrollmentStatus == "Enrolled" && u.EnrollmentExpirationDate <= now.AddDays(7) && u.EnrollmentExpirationDate > now)
				.CountAsync();

			return Ok(new
			{
				totalWorkstations,
				offlineWorkstations,
				successfulLoginsToday,
				failedLoginsToday,
				expiringEnrollments
			});
		}

		[HttpGet("events")]
		public async Task<IActionResult> GetRecentEvents([FromQuery] int limit = 50)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity");
			if (!entitlement.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			limit = Math.Clamp(limit, 1, MaximumRecentEventLimit);
			var events = await _dbContext.AuthenticationEvents
				.Where(e => e.OrganizationId == organizationId)
				.Include(e => e.User)
				.OrderByDescending(e => e.Timestamp)
				.Take(limit)
				.Select(e => new
				{
					id = e.Id,
					timestamp = e.Timestamp,
					providerType = e.ProviderType,
					eventType = e.EventType,
					result = e.Result,
					reason = AuditRedactor.Redact(e.Reason),
					user = e.User != null && e.User.OrganizationId == organizationId ? e.User.DisplayName : "Unknown",
					ipAddress = e.IpAddress
				})
				.ToListAsync();

			return Ok(events);
		}

	}
}
