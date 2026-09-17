using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AnsDelegationController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly IOrganizationContext _organization;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public AnsDelegationController(
			AppDbContext context,
			IOrganizationContext organization,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_context = context;
			_organization = organization;
			_licenseGuard = licenseGuard;
		}

		public class PairNfcRequest
		{
			public Guid UserId { get; set; }
			public string RppsNumber { get; set; } = string.Empty;
			public string NfcBadgeUid { get; set; } = string.Empty;
			public string VerificationMethod { get; set; } = string.Empty; // e.g. "ProSanteConnect"
		}

		[HttpPost("pair-nfc")]
		[Authorize(Policy = "AdminOnly")]
		public Task<IActionResult> PairNfcWithRpps([FromBody] PairNfcRequest request)
		{
			return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status501NotImplemented,
				new { code = "ANS_MTLS_INTEGRATION_REQUIRED", message = "No badge was paired and no declaration was reported as successful." }));
		}

		[HttpGet("logs")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetDelegationLogs()
		{
			if (!_organization.OrganizationId.HasValue) return Forbid();
			var entitlement = await _licenseGuard.HasFeatureAsync(_organization.OrganizationId.Value, "identity");
			if (!entitlement.Allowed)
				return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			var logs = await _context.AnsDelegationLogs
				.Where(l => l.OrganizationId == _organization.OrganizationId.Value)
				.Include(l => l.User)
				.Include(l => l.AdminUser)
				.OrderByDescending(l => l.PairedAt)
				.Select(l => new
				{
					l.Id,
					l.RppsNumber,
					l.VerificationMethod,
					l.PairedAt,
					l.IsDeclaredToGovernment,
					UserName = l.User != null && l.User.OrganizationId == _organization.OrganizationId.Value
						? (string.IsNullOrWhiteSpace(l.User.FirstName) && string.IsNullOrWhiteSpace(l.User.LastName)
							? (l.User.DisplayName ?? l.User.SamAccountName)
							: $"{l.User.FirstName} {l.User.LastName}").Trim()
						: "Unknown",
					CurrentRpps = l.User != null && l.User.OrganizationId == _organization.OrganizationId.Value ? l.User.RppsNumber : string.Empty,
					AdminName = l.AdminUser != null && l.AdminUser.OrganizationId == _organization.OrganizationId.Value
						? (string.IsNullOrWhiteSpace(l.AdminUser.FirstName) && string.IsNullOrWhiteSpace(l.AdminUser.LastName)
							? (l.AdminUser.DisplayName ?? l.AdminUser.SamAccountName)
							: $"{l.AdminUser.FirstName} {l.AdminUser.LastName}").Trim()
						: "System"
				})
				.ToListAsync();

			return Ok(logs);
		}
	}
}
