using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Mail;
using System.Security.Claims;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.API.Services;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize(Policy = "SystemAdminOnly")]
	public class SettingsController : ControllerBase
	{
		private readonly string _settingsFilePath;
		private readonly IAuditService _auditService;
		private readonly IConfiguration _configuration;
		private readonly AppDbContext _db;
		private readonly ILdapService _ldap;
		private readonly IOrganizationContext _organizationContext;
		private readonly IGlobalSettingsOperationProcessor? _settingsOperations;

		public SettingsController(IWebHostEnvironment env, IAuditService auditService, IConfiguration configuration,
			AppDbContext db, ILdapService ldap, IOrganizationContext organizationContext,
			IGlobalSettingsOperationProcessor? settingsOperations = null)
		{
			_auditService = auditService;
			_configuration = configuration;
			_db = db;
			_ldap = ldap;
			_organizationContext = organizationContext;
			_settingsOperations = settingsOperations;
			_settingsFilePath = Path.Combine(env.ContentRootPath, "custom_settings.json");
		}

		[HttpGet]
		public async Task<IActionResult> GetSettings()
		{
			var scopeFailure = await RequireSingleTenantInstanceAsync();
			if (scopeFailure is not null) return scopeFailure;
			var json = System.IO.File.Exists(_settingsFilePath) ? System.IO.File.ReadAllText(_settingsFilePath) : "{}";
			var options = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};
			var settings = JsonSerializer.Deserialize<AppSettingsDto>(json, options);

			// Backward compatibility / Migration
			if (settings != null)
			{
				// Mask sensitive fields
				// Secrets are supplied by the host secret provider and are never read from or returned by this file API.
				settings.LdapPassword = settings.DbPassword = settings.SmtpPassword = null;
				settings.KioskApiKey = settings.KioskAdminPassword = settings.LicenseKey = null;

				// Backward compatibility / Migration
				if (settings.ManagedApps == null || !settings.ManagedApps.Any())
				{
					settings.ManagedApps = new List<ManagedAppDto>();
					if (!string.IsNullOrEmpty(settings.CheminEmed)) settings.ManagedApps.Add(new ManagedAppDto { Id = "EMED", Name = "EMED", Path = settings.CheminEmed, Icon = "FileText" });
					if (!string.IsNullOrEmpty(settings.CheminHestia)) settings.ManagedApps.Add(new ManagedAppDto { Id = "HESTIA", Name = "HESTIA", Path = settings.CheminHestia, Icon = "Database" });
					if (!string.IsNullOrEmpty(settings.CheminSigems)) settings.ManagedApps.Add(new ManagedAppDto { Id = "SIGEMS", Name = "SIGEMS", Path = settings.CheminSigems, Icon = "Cpu" });
					if (!string.IsNullOrEmpty(settings.CheminBlueKango)) settings.ManagedApps.Add(new ManagedAppDto { Id = "BLUE_KANGO", Name = "BLUE KANGO", Path = settings.CheminBlueKango, Icon = "Shield" });
				}
			}

			return Ok(settings!);
		}

		[HttpGet("availability")]
		public async Task<IActionResult> GetSettingsAvailability()
		{
			var organizationCount = await _db.Organizations.IgnoreQueryFilters().CountAsync();
			return Ok(new
			{
				available = organizationCount <= 1,
				message = organizationCount > 1
					? "Les paramètres globaux sont désactivés lorsqu'une instance partage plusieurs organisations."
					: "Les paramètres globaux sont disponibles."
			});
		}

		[HttpPost]
		public async Task<IActionResult> SaveSettings([FromBody] AppSettingsDto settings)
		{
			var scopeFailure = await RequireSingleTenantInstanceAsync();
			if (scopeFailure is not null) return scopeFailure;
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};
			// Never persist credentials or license keys to the content directory.
			settings.LdapPassword = settings.DbPassword = settings.SmtpPassword = null;
			settings.KioskApiKey = settings.KioskAdminPassword = settings.LicenseKey = null;
			settings.LdapUsername = settings.DbUsername = settings.SmtpUsername = null;

			var json = JsonSerializer.Serialize(settings, options);
			try
			{
				if (_settingsOperations is null)
					throw new InvalidOperationException("Durable global settings operation recovery is not configured.");
				await _settingsOperations.CommitAsync(json, _auditService);
			}
			catch
			{
				return StatusCode(StatusCodes.Status503ServiceUnavailable,
					new { code = "SETTINGS_UPDATE_UNCONFIRMED", message = "The settings update could not be confirmed. A durable pending update will be recovered before the API serves requests." });
			}

			return Ok(new { message = "Paramètres mis à jour avec succès" });
		}


		public class DbTestRequest
		{
			public string? DbHost { get; set; }
			public string? DbPort { get; set; }
			public string? DbName { get; set; }
			public string? DbUsername { get; set; }
			public string? DbPassword { get; set; }
		}

		[HttpPost("test-db")]
		public async Task<IActionResult> TestDatabaseConnection(CancellationToken cancellationToken)
		{
			var scopeFailure = await RequireSingleTenantInstanceAsync();
			if (scopeFailure is not null) return scopeFailure;
			try
			{
				var connected = await _db.Database.CanConnectAsync(cancellationToken);
				return Ok(new { success = connected, message = connected ? "Configured database is reachable." : "Configured database is unavailable." });
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				return Ok(new { success = false, message = "Configured database is unavailable." });
			}
		}

		public class LdapTestRequest
		{
			public string? LdapUrl { get; set; }
			public string? LdapBaseDn { get; set; }
			public string? LdapUsername { get; set; }
			public string? LdapPassword { get; set; }
		}

		[HttpPost("test-ldap")]
		public async Task<IActionResult> TestLdapConnection(CancellationToken cancellationToken)
		{
			var scopeFailure = await RequireSingleTenantInstanceAsync();
			if (scopeFailure is not null) return scopeFailure;
			var connected = await _ldap.TestConnectionAsync();
			return Ok(new { success = connected, message = connected ? "Configured LDAPS service is reachable." : "Configured LDAPS service is unavailable." });
		}

		public class TestEmailRequest
		{
			public string? SmtpHost { get; set; }
			public string? SmtpPort { get; set; }
			public string? SmtpUsername { get; set; }
			public string? SmtpPassword { get; set; }
			public bool SmtpEnableSsl { get; set; }
			public string? SmtpFromEmail { get; set; }
			public string? TargetEmail { get; set; }
		}

		[HttpPost("test-email")]
		public async Task<IActionResult> TestEmailConnection([FromBody] TestEmailRequest? req, CancellationToken cancellationToken)
		{
			var scopeFailure = await RequireSingleTenantInstanceAsync();
			if (scopeFailure is not null) return scopeFailure;
			try
			{
				var host = (req?.SmtpHost ?? _configuration["Smtp:Host"])?.Trim();
				var username = (req?.SmtpUsername ?? _configuration["Smtp:Username"])?.Trim();
				var password = (req?.SmtpPassword ?? _configuration["Smtp:Password"])?.Trim();
				var from = (req?.SmtpFromEmail ?? _configuration["Smtp:From"])?.Trim();
				var recipient = (req?.TargetEmail ?? _configuration["Smtp:TestRecipient"])?.Trim();
				
				var portStr = req?.SmtpPort ?? _configuration["Smtp:Port"];
				var enableSsl = req?.SmtpEnableSsl ?? true;

				if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) ||
					string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(recipient) ||
					!int.TryParse(portStr, out var port))
					return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "Secure SMTP test is not configured." });
				using var client = new SmtpClient(host, port)
				{
					UseDefaultCredentials = false,
					Credentials = new System.Net.NetworkCredential(username, password),
					EnableSsl = enableSsl,
					Timeout = 8000
				};
				using var mailMessage = new MailMessage
				{
					From = new MailAddress(from),
					Subject = "NexorSys SMTP configuration test",
					Body = "This message verifies the configured SMTP transport."
				};
				mailMessage.To.Add(recipient);
				await client.SendMailAsync(mailMessage, cancellationToken);
				return Ok(new { success = true, message = "Configured SMTP test completed." });
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				return Ok(new { success = false, message = $"Configured SMTP test failed: {ex.InnerException?.Message ?? ex.Message}" });
			}
		}

		private async Task<IActionResult?> RequireSingleTenantInstanceAsync()
		{
			int organizationCount;
			try
			{
				organizationCount = await _db.Organizations.IgnoreQueryFilters().CountAsync();
			}
			catch
			{
				return StatusCode(StatusCodes.Status503ServiceUnavailable,
					new { code = "INSTANCE_SCOPE_UNAVAILABLE", message = "Instance configuration scope could not be verified." });
			}

			return organizationCount > 1
				? Conflict(new { code = "INSTANCE_GLOBAL_SETTINGS_SINGLE_TENANT_ONLY", message = "Global instance settings are unavailable when multiple organizations share this deployment." })
				: null;
		}

		[HttpPost("activate-license")]
		public async Task<IActionResult> ActivateLicense([FromBody] LicenseActivationRequest req)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var revocation = SignedLicenseRevocationSnapshotVerifier.Verify(_configuration["Licensing:RevocationSnapshot"],
				_configuration["Licensing:PublicKeyPem"], DateTimeOffset.UtcNow);
			if (!revocation.IsValid || revocation.Snapshot is null)
				return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, code = revocation.Code, message = "A current vendor-signed revocation snapshot is required." });
			var result = SignedLicenseVerifier.Verify(req?.LicenseKey, _configuration["Licensing:PublicKeyPem"],
				organizationId, _configuration["Licensing:Product"] ?? "NexorSys Identity + Kiosk",
				_configuration["Licensing:ProductVersion"] ?? "1.5.0",
				new HashSet<string>(revocation.Snapshot.RevokedLicenseIds, StringComparer.Ordinal), DateTimeOffset.UtcNow);
			if (!result.IsValid || result.Claims is null)
				return BadRequest(new { success = false, code = result.Code, message = "License verification failed; no entitlement was activated." });

			var claims = result.Claims;
			var license = await _db.OrganizationLicenses.FirstOrDefaultAsync(x => x.OrganizationId == organizationId);
			if (license is null)
			{
				license = new Nexorsys.Identity.Core.OrganizationLicense { Id = Guid.NewGuid(), OrganizationId = organizationId };
				_db.OrganizationLicenses.Add(license);
			}
			license.LicensedUsers = claims.LicensedUsers;
			license.LicensedWorkstations = claims.LicensedWorkstations;
			license.LicensedModules = JsonSerializer.Serialize(claims.Modules);
			license.LicenseId = claims.LicenseId;
			license.SignedLicenseToken = req!.LicenseKey;
			license.HighestRevocationSequence = revocation.Snapshot.Sequence;
			license.NotBefore = claims.NotBefore.UtcDateTime;
			license.ExpiryDate = claims.ExpiresAt.UtcDateTime;
			license.IsTrial = claims.IsTrial;
			license.SupportLevel = claims.IsTrial ? "Trial" : "Commercial";
			var actorId = TenantActorOwnership.ParseAndEnsure(_db, User, organizationId);
			_db.AuditLogs.Add(new AuditLog
			{
				Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId,
				Action = "Signed license activated", ResourceType = "OrganizationLicense", ResourceId = license.Id,
				NewValues = AuditRedactor.Redact($"LicenseId={claims.LicenseId}; Users={claims.LicensedUsers}; Workstations={claims.LicensedWorkstations}; Modules={string.Join(',', claims.Modules)}; Expires={claims.ExpiresAt:O}"),
				IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
				UserAgent = "api", CreatedAt = DateTime.UtcNow
			});
			await _db.SaveChangesAsync();
			return Ok(new { success = true, code = result.Code, expiresAt = license.ExpiryDate, licensedUsers = license.LicensedUsers, licensedWorkstations = license.LicensedWorkstations, modules = claims.Modules });
		}
	}

	public class ManagedAppDto
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public string Icon { get; set; } = "Monitor";
		public string Description { get; set; } = string.Empty;
	}

	public sealed class KioskManagedAppDto
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Icon { get; set; } = "Monitor";
		public string Description { get; set; } = string.Empty;
	}

	public class AppSettingsDto
	{
		public string? LdapUrl { get; set; }
		public string? LdapBaseDn { get; set; }
		public string? LdapUsername { get; set; }
		public string? LdapPassword { get; set; }
		public List<ManagedAppDto> ManagedApps { get; set; } = new();

		// Legacy fields (kept for binding compatibility during transition)
		public string? CheminEmed { get; set; }
		public string? CheminHestia { get; set; }
		public string? CheminSigems { get; set; }
		public string? CheminBlueKango { get; set; }

		public int MaxTentatives { get; set; } = 5;
		public int DureeBlocage { get; set; } = 15;
		public int DureeExpiration { get; set; } = 30;
		public bool VerrouillageAuto { get; set; } = true;
		public string? ApiAnsUrl { get; set; }
		public string? DbHost { get; set; }
		public string? DbPort { get; set; }
		public string? DbName { get; set; }
		public string? DbUsername { get; set; }
		public string? DbPassword { get; set; }
		public string? KioskApiKey { get; set; }
		public string? KioskUrl { get; set; }
		public string? KioskAdminPassword { get; set; }

		public string? LicenseExpiry { get; set; }
		public string? LicenseKey { get; set; }

		// MIE Device Activation Toggles
		public bool MieNfcEnabled { get; set; } = true;
		public bool MieCpsEnabled { get; set; } = true;
		public bool MieFidoEnabled { get; set; } = true;
		public bool MieCartePsEnabled { get; set; } = true;
		public bool MiePsiEnabled { get; set; } = true;

		// NexorSys-HR Integration Configuration


		// SMTP settings configuration
		public string? SmtpHost { get; set; }
		public int SmtpPort { get; set; } = 587;
		public string? SmtpUsername { get; set; }
		public string? SmtpPassword { get; set; }
		public bool SmtpEnableSsl { get; set; } = true;
		public string? SmtpFromEmail { get; set; }
	}

	public class LicenseActivationRequest
	{
		public string LicenseKey { get; set; } = string.Empty;
	}
}
