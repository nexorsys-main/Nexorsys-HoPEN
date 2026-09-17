using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Npgsql;


namespace Nexorsys.Identity.API.Controllers
{
	/// <summary>
	/// MIE (Moyens d'Identification Électronique) Device Management Controller.
	/// Manages all PSI-compatible identity devices per user:
	/// NFC Badge, CPS, eCPS, FIDO2, Carte PS, PSI tokens.
	/// The existing Kiosk endpoints for badge management remain unchanged.
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	[Authorize(Policy = "AdminOnly")]
	public class DevicesController : ControllerBase
	{
		private const string ActiveCredentialIndexName = "ux_user_devices_org_type_identifier_active";
		private readonly AppDbContext _context;
		private readonly IAuditService _auditService;
		private readonly ILogger<DevicesController> _logger;
		private readonly IOrganizationContext _organization;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public DevicesController(AppDbContext context, IAuditService auditService, ILogger<DevicesController> logger, IOrganizationContext organization,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_context = context;
			_auditService = auditService;
			_logger = logger;
			_organization = organization;
			_licenseGuard = licenseGuard;
		}

		/// <summary>
		/// Get all devices for a specific user.
		/// </summary>
		[HttpGet("user/{userId}")]
		public async Task<IActionResult> GetUserDevices(Guid userId)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var devices = await _context.UserDevices
				.Where(d => d.OrganizationId == _organization.OrganizationId.GetValueOrDefault() && d.UserId == userId &&
					d.User != null && d.User.OrganizationId == _organization.OrganizationId.GetValueOrDefault())
				.OrderByDescending(d => d.IsPrimary)
				.ThenBy(d => d.DeviceType)
				.Select(d => new { d.Id, d.UserId, d.DeviceType, d.DeviceName, CredentialRegistered = true, d.Status, d.AssuranceLevel, d.IsPrimary, d.IsCertified, d.ExpiresAt, d.LastUsedAt, d.CreatedAt })
				.ToListAsync();

			return Ok(devices);
		}

		/// <summary>
		/// Get all devices across all users (admin overview).
		/// </summary>
		[HttpGet("all")]
		public async Task<IActionResult> GetAllDevices(
			[FromQuery] string? type = null,
			[FromQuery] string? status = null)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var query = _context.UserDevices
				.Include(d => d.User)
				.Where(d => d.OrganizationId == _organization.OrganizationId.GetValueOrDefault() && d.User != null && d.User.OrganizationId == _organization.OrganizationId.GetValueOrDefault())
				.AsQueryable();

			if (!string.IsNullOrEmpty(type))
				query = query.Where(d => d.DeviceType == type);
			if (!string.IsNullOrEmpty(status))
				query = query.Where(d => d.Status == status);

			var devices = await query
				.OrderByDescending(d => d.CreatedAt)
				.Take(200)
				.Select(d => new
				{
					d.Id,
					d.UserId,
					UserDisplayName = d.User != null ? d.User.DisplayName : null,
					UserDepartment = d.User != null ? d.User.Department : null,
					d.DeviceType,
					d.DeviceName,
					CredentialRegistered = true,
					d.DeviceSerial,
					d.Status,
					d.AssuranceLevel,
					d.IsPrimary,
					d.IsCertified,
					d.CertificationReference,
					d.ExpiresAt,
					d.LastUsedAt,
					d.CreatedAt
				})
				.ToListAsync();

			return Ok(devices);
		}

		/// <summary>
		/// Register a new identity device (MIE) for a user.
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var organizationId = _organization.OrganizationId.GetValueOrDefault();
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.OrganizationId == organizationId);
			if (user == null) return NotFound(new { message = "Utilisateur introuvable." });
			if (string.IsNullOrWhiteSpace(request.DeviceIdentifier) || request.DeviceIdentifier.Length > 512 || string.IsNullOrWhiteSpace(request.DeviceType)) return BadRequest();
			if (await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context, organizationId, user.Id, request.DeviceIdentifier) ||
				(!string.IsNullOrWhiteSpace(request.DeviceSerial) && await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context, organizationId, user.Id, request.DeviceSerial)))
				return Conflict(new { code = "CREDENTIAL_ALREADY_OWNED" });
			if (await _context.UserDevices.IgnoreQueryFilters().AnyAsync(d => d.OrganizationId == organizationId && d.DeviceType == request.DeviceType && d.DeviceIdentifier == request.DeviceIdentifier && d.Status != "revoked")) return Conflict("Credential is already registered.");

			// If this is an NFC_Badge, sync with User.BadgeUid for backward compatibility
			if (request.DeviceType == "NFC_Badge" && !string.IsNullOrEmpty(request.DeviceIdentifier))
			{
				user.BadgeUid = request.DeviceIdentifier;
				user.UpdatedAt = DateTime.UtcNow;
			}

			var device = new UserDevice
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				UserId = request.UserId,
				DeviceType = request.DeviceType,
				DeviceName = request.DeviceName,
				DeviceIdentifier = request.DeviceIdentifier,
				DeviceSerial = request.DeviceSerial,
				Status = "active",
				AssuranceLevel = GetAssuranceLevel(request.DeviceType),
				IsPrimary = request.IsPrimary,
				IsCertified = false,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			// If setting as primary, deactivate other primary devices of same type
			if (request.IsPrimary)
			{
				var existingPrimary = await _context.UserDevices
					.Where(d => d.OrganizationId == organizationId && d.UserId == request.UserId && d.DeviceType == request.DeviceType && d.IsPrimary)
					.ToListAsync();
				foreach (var ep in existingPrimary) ep.IsPrimary = false;
			}

			await _context.UserDevices.AddAsync(device);
			_auditService.Add(
				action: "Enregistrement d'un nouveau MIE",
				resourceType: "Équipement",
				resourceId: device.Id,
				newValues: $"Type: {device.DeviceType}, credential value omitted."
			);
			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (IsActiveCredentialConflict(ex))
			{
				_logger.LogWarning("Concurrent MIE credential registration was rejected by the tenant uniqueness constraint.");
				return Conflict("Credential is already registered.");
			}

			return Ok(SafeDevice(device));
		}

		/// <summary>
		/// Update device status (active, suspended, revoked).
		/// </summary>
		[HttpPut("{deviceId}/status")]
		public async Task<IActionResult> UpdateDeviceStatus(Guid deviceId, [FromBody] UpdateStatusRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (request.Status is not ("active" or "suspended" or "revoked")) return BadRequest("Unsupported device state.");
			var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.OrganizationId == _organization.OrganizationId.GetValueOrDefault());
			if (device == null) return NotFound();
			if (request.Status == "active" &&
				(await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context, device.OrganizationId, device.UserId, device.DeviceIdentifier) ||
				 (!string.IsNullOrWhiteSpace(device.DeviceSerial) && await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context, device.OrganizationId, device.UserId, device.DeviceSerial))))
				return Conflict(new { code = "CREDENTIAL_ALREADY_OWNED" });

			device.Status = request.Status;
			device.UpdatedAt = DateTime.UtcNow;

			// If revoking an NFC badge, also clear User.BadgeUid for backward compat
			if (request.Status == "revoked" && device.DeviceType == "NFC_Badge")
			{
				var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == device.UserId && u.OrganizationId == device.OrganizationId);
				if (user != null && user.BadgeUid == device.DeviceIdentifier)
				{
					user.BadgeUid = null;
					user.UpdatedAt = DateTime.UtcNow;
				}
			}

			_auditService.Add(
				action: "Mise à jour du statut MIE",
				resourceType: "Équipement",
				resourceId: device.Id,
				newValues: $"NouveauStatut: {request.Status}, Type: {device.DeviceType}"
			);
			await _context.SaveChangesAsync();

			return Ok(SafeDevice(device));
		}

		/// <summary>
		/// Set PSI certification status on a device.
		/// </summary>
		[HttpPut("{deviceId}/certify")]
		public async Task<IActionResult> CertifyDevice(Guid deviceId, [FromBody] CertifyRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var certificationReference = request.CertificationReference?.Trim();
			if (request.IsCertified && string.IsNullOrWhiteSpace(certificationReference))
				return BadRequest("A manually verified certification reference is required.");
			if ((certificationReference?.Length ?? 0) > 256 ||
				(certificationReference?.Any(char.IsControl) ?? false))
				return BadRequest("Certification reference is invalid.");
			var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.OrganizationId == _organization.OrganizationId.GetValueOrDefault());
			if (device == null) return NotFound();

			device.IsCertified = request.IsCertified;
			device.CertificationReference = request.IsCertified ? certificationReference : null;
			device.UpdatedAt = DateTime.UtcNow;
			_auditService.Add(
				action: "Certification PSI du MIE",
				resourceType: "Équipement",
				resourceId: device.Id,
				newValues: $"Manual administrator attestation: IsCertified={request.IsCertified}; Reference={certificationReference ?? "(cleared)"}. Reference is not externally verified by this API."
			);
			await _context.SaveChangesAsync();

			return Ok(SafeDevice(device));
		}

		/// <summary>
		/// Delete a device registration permanently.
		/// </summary>
		[HttpDelete("{deviceId}")]
		public async Task<IActionResult> DeleteDevice(Guid deviceId)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;

			var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.OrganizationId == _organization.OrganizationId.GetValueOrDefault());
			if (device == null)
			{
				_logger.LogWarning("Device {DeviceId} not found for deletion", deviceId);
				return NotFound();
			}

			// Preserve a tombstone so revoked credentials can never fall back to legacy matching.
			device.Status = "revoked";
			device.UpdatedAt = DateTime.UtcNow;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == device.UserId && u.OrganizationId == device.OrganizationId);
			if (user != null)
			{
				bool modified = false;
				if (device.DeviceType == "NFC_Badge" && user.BadgeUid == device.DeviceIdentifier)
				{
					user.BadgeUid = null;
					modified = true;
				}
				else if (device.DeviceType == "CPS" && user.CpsId == device.DeviceIdentifier)
				{
					user.CpsId = null;
					modified = true;
				}
				else if (device.DeviceType == "FIDO2" && user.FidoId == device.DeviceIdentifier)
				{
					user.FidoId = null;
					modified = true;
				}

				if (modified)
				{
					user.UpdatedAt = DateTime.UtcNow;
					_logger.LogInformation("Cleared master ID for user {UserId} after deleting device {DeviceId}", device.UserId, deviceId);
				}
			}

			// Do not delete device records: they are revocation evidence.
			_auditService.Add(
				action: "Suppression définitive du MIE",
				resourceType: "Équipement",
				resourceId: deviceId,
				newValues: $"Type: {device.DeviceType}, Status: revoked; credential value omitted."
			);
			await _context.SaveChangesAsync();

			return NoContent();
		}

		/// <summary>
		/// Get device statistics for the dashboard.
		/// </summary>
		[HttpGet("stats")]
		public async Task<IActionResult> GetDeviceStats()
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var organizationId = _organization.OrganizationId.GetValueOrDefault();
			var all = await _context.UserDevices
				.Where(d => d.OrganizationId == organizationId && d.User != null && d.User.OrganizationId == organizationId)
				.ToListAsync();
			var stats = new
			{
				Total = all.Count,
				Active = all.Count(d => d.Status == "active"),
				Suspended = all.Count(d => d.Status == "suspended"),
				Revoked = all.Count(d => d.Status == "revoked"),
				Certified = all.Count(d => d.IsCertified),
				PendingCertification = all.Count(d => d.Status == "pending_certification"),
				ByType = all.GroupBy(d => d.DeviceType).Select(g => new
				{
					Type = g.Key,
					Count = g.Count(),
					ActiveCount = g.Count(d => d.Status == "active")
				})
			};
			return Ok(stats);
		}

		private static object SafeDevice(UserDevice d) => new { d.Id, d.UserId, d.DeviceType, d.DeviceName, CredentialRegistered = true, d.DeviceSerial, d.Status, d.AssuranceLevel, d.IsPrimary, d.IsCertified, d.CertificationReference, d.ExpiresAt, d.LastUsedAt, d.CreatedAt };

		private async Task<IActionResult?> RequireIdentityEntitlementAsync()
		{
			if (!_organization.OrganizationId.HasValue) return Forbid();
			var decision = await _licenseGuard.HasFeatureAsync(_organization.OrganizationId.GetValueOrDefault(), "identity");
			return decision.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = decision.Code });
		}

		private static bool IsActiveCredentialConflict(Exception exception)
		{
			for (Exception? current = exception; current is not null; current = current.InnerException)
			{
				if (current is PostgresException postgres &&
					postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
					postgres.ConstraintName == ActiveCredentialIndexName)
					return true;
			}
			return false;
		}

		private static string GetAssuranceLevel(string deviceType) => deviceType switch
		{
			"NFC_Badge" => "Intermediate",
			"CPS" => "High",
			"eCPS" => "High",
			"FIDO2" => "High",
			"CartePS" => "High",
			"PSI" => "Enhanced",
			_ => "Standard"
		};
	}

	// DTO classes
	public class RegisterDeviceRequest
	{
		public Guid UserId { get; set; }
		public string DeviceType { get; set; } = string.Empty;
		public string DeviceName { get; set; } = string.Empty;
		public string? DeviceIdentifier { get; set; }
		public string? DeviceSerial { get; set; }
		public bool IsPrimary { get; set; }
	}

	public class UpdateStatusRequest
	{
		public string Status { get; set; } = string.Empty;
	}

	public class CertifyRequest
	{
		public bool IsCertified { get; set; }
		public string? CertificationReference { get; set; }
	}
}
