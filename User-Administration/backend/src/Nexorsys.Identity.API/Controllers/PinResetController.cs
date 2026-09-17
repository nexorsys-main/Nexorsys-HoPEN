using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/pin-reset")]
	[Authorize]
	public class PinResetController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public PinResetController(AppDbContext dbContext, IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpPost("request")]
		public async Task<IActionResult> RequestReset([FromBody] PinResetRequestDto request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == actorId && u.OrganizationId == _organizationContext.OrganizationId.Value && u.IsActive);
			if (user == null) return NotFound();

			var resetReq = new PinResetRequest
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organizationContext.OrganizationId.Value,
				UserId = user.Id,
				BadgeUid = string.Empty,
				RequestedAt = DateTime.UtcNow,
				Status = "Pending",
				Reason = SanitizeReason(request.Reason)
			};

			_dbContext.PinResetRequests.Add(resetReq);

			_dbContext.AuthenticationEvents.Add(new AuthenticationEvent
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organizationContext.OrganizationId.Value,
				Timestamp = DateTime.UtcNow,
				UserId = user.Id,
				ProviderType = "System",
				EventType = "PIN_RESET_REQUESTED",
				Result = "Success"
			});

			await _dbContext.SaveChangesAsync();

			return Ok(new { message = "PIN reset request submitted.", id = resetReq.Id });
		}

		[HttpGet("pending")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetPendingRequests()
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var requests = await _dbContext.PinResetRequests
				.Include(r => r.User)
				.Where(r => r.OrganizationId == _organizationContext.OrganizationId.Value && r.Status == "Pending")
				.OrderByDescending(r => r.RequestedAt)
				.Select(r => new
				{
					r.Id,
					r.UserId,
					UserName = r.User != null && r.User.OrganizationId == _organizationContext.OrganizationId.Value
						? (r.User.DisplayName ?? r.User.SamAccountName) : "Unknown",
					r.RequestedAt,
					Reason = AuditRedactor.Redact(r.Reason),
					r.Status
				})
				.ToListAsync();

			return Ok(requests);
		}

		[HttpPost("approve/{id}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> Approve(Guid id)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var request = await _dbContext.PinResetRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == _organizationContext.OrganizationId.Value);
			if (request == null) return NotFound();
			if (request.Status != "Pending") return Conflict(new { code = "PIN_RESET_ALREADY_DECIDED" });

			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.OrganizationId == request.OrganizationId);
			if (user == null) return NotFound("User not found.");

			var decidedAt = DateTime.UtcNow;
			var decidedBy = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(
				_dbContext, User, request.OrganizationId);
			await using var transaction = _dbContext.Database.IsRelational()
				? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
				: null;
			if (_dbContext.Database.IsRelational())
			{
				var changed = await _dbContext.PinResetRequests.Where(r => r.Id == id &&
					r.OrganizationId == request.OrganizationId && r.Status == "Pending")
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(r => r.Status, "Approved")
						.SetProperty(r => r.ApprovedAt, decidedAt)
						.SetProperty(r => r.ApprovedBy, decidedBy));
				if (changed != 1) return Conflict(new { code = "PIN_RESET_ALREADY_DECIDED" });
				request.Status = "Approved";
				request.ApprovedAt = decidedAt;
				request.ApprovedBy = decidedBy;
			}
			else
			{
				request.Status = "Approved";
				request.ApprovedAt = decidedAt;
				request.ApprovedBy = decidedBy;
			}

			user.MustChangePin = true;
			AddDecisionEvent(request, user.Id, "PIN_RESET_APPROVED", decidedAt);
			await _dbContext.SaveChangesAsync();
			if (transaction is not null) await transaction.CommitAsync();

			return Ok(new { message = "Approved. User will be forced to change PIN at next login." });
		}

		[HttpPost("reject/{id}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> Reject(Guid id)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var request = await _dbContext.PinResetRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == _organizationContext.OrganizationId.Value);
			if (request == null) return NotFound();
			if (request.Status != "Pending") return Conflict(new { code = "PIN_RESET_ALREADY_DECIDED" });
			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.OrganizationId == request.OrganizationId);
			if (user == null) return NotFound();

			var decidedAt = DateTime.UtcNow;
			var decidedBy = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(
				_dbContext, User, request.OrganizationId);
			await using var transaction = _dbContext.Database.IsRelational()
				? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
				: null;
			if (_dbContext.Database.IsRelational())
			{
				var changed = await _dbContext.PinResetRequests.Where(r => r.Id == id &&
					r.OrganizationId == request.OrganizationId && r.Status == "Pending")
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(r => r.Status, "Rejected")
						.SetProperty(r => r.ApprovedAt, decidedAt)
						.SetProperty(r => r.ApprovedBy, decidedBy));
				if (changed != 1) return Conflict(new { code = "PIN_RESET_ALREADY_DECIDED" });
				request.Status = "Rejected";
				request.ApprovedAt = decidedAt;
				request.ApprovedBy = decidedBy;
			}
			else
			{
				request.Status = "Rejected";
				request.ApprovedAt = decidedAt;
				request.ApprovedBy = decidedBy;
			}

			AddDecisionEvent(request, user.Id, "PIN_RESET_REJECTED", decidedAt);
			await _dbContext.SaveChangesAsync();
			if (transaction is not null) await transaction.CommitAsync();

			return Ok(new { message = "Rejected." });
		}

		private async Task<IActionResult?> RequireKioskEntitlementAsync()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var decision = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
			return decision.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = decision.Code });
		}

		private static string SanitizeReason(string? reason)
		{
			var value = string.IsNullOrWhiteSpace(reason) ? "Forgot PIN" : reason.Trim();
			value = value[..Math.Min(value.Length, 256)];
			return AuditRedactor.Redact(value) ?? "Forgot PIN";
		}

		private void AddDecisionEvent(PinResetRequest request, Guid userId, string eventType, DateTime timestamp) =>
			_dbContext.AuthenticationEvents.Add(new AuthenticationEvent
			{
				Id = Guid.NewGuid(), OrganizationId = request.OrganizationId, Timestamp = timestamp, UserId = userId,
				ProviderType = "System", EventType = eventType, Result = "Success"
			});
	}

	public class PinResetRequestDto
	{
		public string? Reason { get; set; }
	}
}
