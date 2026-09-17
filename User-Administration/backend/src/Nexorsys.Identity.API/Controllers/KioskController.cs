using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.API.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Nexorsys.Identity.API.Controllers;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Nexorsys.Identity.API.Hubs;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[ApiKeyAuth] // Secured with API Key for Kiosks
	[EnableRateLimiting(Nexorsys.Identity.API.Services.ApiRateLimitPolicies.Kiosk)]
	public class KioskController : ControllerBase
	{
		private readonly IKioskService _kioskService;
		private readonly IAuditService _auditService;
		private readonly ILogger<KioskController> _logger;
		private readonly Nexorsys.Identity.API.Services.ActiveKioskService _activeKioskService;
		private readonly AppDbContext _context;
		private readonly IHubContext<IdentityHub> _hubContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public KioskController(
			IKioskService kioskService,
			IAuditService auditService,
			ILogger<KioskController> logger,
			Nexorsys.Identity.API.Services.ActiveKioskService activeKioskService,
			AppDbContext context,
			IHubContext<IdentityHub> hubContext,
			IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_kioskService = kioskService;
			_auditService = auditService;
			_logger = logger;
			_activeKioskService = activeKioskService;
			_context = context;
			_hubContext = hubContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpPost("identify")]
		[AllowAnonymous]
		public async Task<IActionResult> Identify([FromBody] KioskIdentifyRequest? request)
		{
			try
			{
				if (request == null) return BadRequest("Invalid request body");

				return await IdentifyInternal(new KioskMieIdentifyRequest
				{
					Identifier = request.BadgeUid ?? string.Empty,
					MachineName = request.MachineName ?? string.Empty,
					Type = null // Allow searching across all device types (NFC, CPS, etc.)
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CRASH in Identify");
				return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "The request could not be completed.");
			}
		}

		[HttpPost("identify-mie")]
		[AllowAnonymous]
		public async Task<IActionResult> IdentifyMie([FromBody] KioskMieIdentifyRequest? request)
		{
			try
			{
				if (request == null) return BadRequest("Invalid request body");
				return await IdentifyInternal(request);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "CRASH in IdentifyMie");
				return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "The request could not be completed.");
			}
		}

		private async Task<IActionResult> IdentifyInternal(KioskMieIdentifyRequest request)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var license = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
			if (!license.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = license.Code });
			if (!await IsTrustedWorkstationAsync(request.MachineName))
				return Unauthorized("Kiosk workstation is not enrolled or trusted.");
			if (!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var trustedWorkstationId)) return Unauthorized();
			_logger.LogInformation("Kiosk MIE identification attempt: {Type} on {MachineName}",
				request.Type ?? "Any", request.MachineName);

			if (!string.IsNullOrEmpty(request.MachineName))
			{
				_activeKioskService.RegisterHeartbeat(_organizationContext.OrganizationId.Value, trustedWorkstationId, request.MachineName);
				HttpContext.Items["CustomUserAgent"] = request.MachineName;
			}

			var user = await _kioskService.IdentifyByMieAsync(request.Identifier, request.Type);
			if (user == null || user.OrganizationId != _organizationContext.OrganizationId.Value)
			{
				await _auditService.LogAsync(
					action: "Échec Identification (Inconnu)",
					resourceType: "Sécurité",
					resourceId: null,
					newValues: $"Unknown credential; machine: {request.MachineName}"
				);
				return NotFound("Identité non trouvée dans le registre");
			}

			if (!user.IsActive)
			{
			_logger.LogWarning("ACCESS BLOCKED: User {UserId} is inactive; machine identifier omitted.", user.Id);

				await _auditService.LogAsync(
					action: "Accès Refusé (Compte Verrouillé)",
					resourceType: "Utilisateur",
					resourceId: user.Id,
					userId: user.Id,
					newValues: $"Credential match denied; type: {request.Type ?? "unspecified"}; workstation: {request.MachineName}"
				);

				return StatusCode(403, new
				{
					Message = "Accès refusé. Votre compte est désactivé ou verrouillé. Veuillez contacter l'administration.",
					IsActive = false
				});
			}

			// Successful identification audit
			await _auditService.LogAsync(
				action: "Identification MIE Réussie",
				resourceType: "Utilisateur",
				resourceId: user.Id,
				userId: user.Id,
				newValues: $"Machine: {request.MachineName}, Type: {request.Type}"
			);
			return Ok(new
			{
				userId = user.Id,
				displayName = user.DisplayName,
				department = user.Department,
				jobTitle = user.Title,
				badgeType = user.BadgeType,
				needsPinReset = user.MustChangePin,
				usedType = request.Type
			});
		}

		[HttpPost("validate-pin")]
		[AllowAnonymous]
		public async Task<IActionResult> ValidatePin([FromBody] KioskPinRequest request)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var license = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
			if (!license.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = license.Code });
			if (!await IsTrustedWorkstationAsync(request.MachineName))
				return Unauthorized("Kiosk workstation is not enrolled or trusted.");
			if (!string.IsNullOrEmpty(request.MachineName))
			{
				HttpContext.Items["CustomUserAgent"] = request.MachineName;
			}

			_logger.LogInformation("Kiosk PIN verification requested for user {UserId}", request.UserId);

			if (!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var workstationId)) return Unauthorized();
			var isValid = await _kioskService.ValidatePinAsync(request.UserId, request.Pin, request.BadgeUid, workstationId);

			if (!isValid)
			{
				var status = await _kioskService.GetUserSecurityStatusAsync(request.UserId);
				bool isLocked = false;
				if (status != null)
				{
					// Use reflection or dynamic to get IsAccountLocked if status is anonymous
					var lockedProp = status.GetType().GetProperty("IsAccountLocked");
					isLocked = (bool)(lockedProp?.GetValue(status) ?? false);
				}

				_logger.LogWarning("Invalid PIN attempt for user {UserId}. Locked: {IsLocked}", request.UserId, isLocked);

				return Unauthorized(isLocked ? "Account locked due to multiple failed attempts." : "Invalid PIN");
			}

			var session = await _kioskService.StartSessionAsync(request.UserId, request.BadgeUid, request.NfcUid, workstationId);
			if (session is null || string.IsNullOrWhiteSpace(session.SessionToken))
			{
				await _auditService.LogAsync(
					action: "Échec création session Kiosque",
					resourceType: "Utilisateur",
					resourceId: request.UserId,
					userId: request.UserId,
					newValues: "PIN accepted but no active session was created."
				);
				return Unauthorized("A session could not be established. Please authenticate again.");
			}

			// Fetch dynamic applications for the Kiosk and filter by user permissions
			List<KioskManagedAppDto> apps = await GetAuthorizedManagedAppsAsync(request.UserId);

			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.OrganizationId == _organizationContext.OrganizationId);

			return Ok(new
			{
				Success = true,
				Token = session?.SessionToken,
				MustChangePin = user?.MustChangePin ?? false,
				User = new
				{
					Id = user?.Id.ToString(),
					Name = user?.DisplayName ?? "Utilisateur",
					SamAccountName = user?.SamAccountName ?? "",
					Department = user?.Department ?? "Clinique"
				},
				Applications = apps
			});
		}

		[HttpPost("validate-sso")]
		[AllowAnonymous]
		public async Task<IActionResult> ValidateSso([FromBody] KioskSsoRequest request)
		{
			// A username supplied by the Kiosk is not proof of a Windows logon.
			return StatusCode(StatusCodes.Status501NotImplemented, new
			{
				message = "Windows logon proof is not configured. No session was created."
			});
		}


		private async Task<List<KioskManagedAppDto>> GetAuthorizedManagedAppsAsync(Guid userId)
		{
			List<KioskManagedAppDto> apps = new();
			try
			{
				var permissionRows = await _context.UserPermissions
					.Where(p => p.UserId == userId && p.OrganizationId == _organizationContext.OrganizationId &&
						p.Application != null && p.Application.OrganizationId == _organizationContext.OrganizationId &&
						(!p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow))
					.Include(p => p.Application)
					.Select(p => new { p.PermissionLevel, p.Application!.ClientId })
					.ToListAsync();
				var authorizedAppIds = permissionRows.Where(p => ApplicationPermissionPolicy.AllowsLaunch(p.PermissionLevel))
					.Select(p => p.ClientId).ToList();

				var settingsPath = "custom_settings.json";
				if (System.IO.File.Exists(settingsPath))
				{
					var json = System.IO.File.ReadAllText(settingsPath);
					var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettingsDto>(json, options);

					if (settings?.ManagedApps != null)
					{
						apps = settings.ManagedApps
							.Where(a => authorizedAppIds.Contains(a.Id, StringComparer.OrdinalIgnoreCase))
							.Select(ToKioskManagedApp)
							.ToList();
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error filtering ManagedApps");
			}
			return apps;
		}

		internal static KioskManagedAppDto ToKioskManagedApp(ManagedAppDto app) => new()
		{
			Id = app.Id,
			Name = app.Name,
			Icon = app.Icon,
			Description = app.Description
		};

		private async Task<bool> IsTrustedWorkstationAsync(string? machineName)
		{
			if (!_organizationContext.OrganizationId.HasValue || string.IsNullOrWhiteSpace(machineName)) return false;
			if (!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var workstationId)) return false;
			return await _context.Workstations.AnyAsync(w =>
				w.Id == workstationId && w.OrganizationId == _organizationContext.OrganizationId.Value &&
				w.IsActive && w.Hostname == machineName);
		}


		[HttpGet("welcome-session/{samAccountName}")]
		[Authorize]
		public async Task<IActionResult> GetWelcomeSession(string samAccountName)
		{
			return StatusCode(StatusCodes.Status501NotImplemented, new
			{
				message = "Windows logon proof and a server-issued session are not configured. No session was created."
			});
		}

		[HttpPost("reset-pin")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> ResetPin([FromBody] KioskResetPinRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue || !await _context.Users.AnyAsync(u => u.Id == request.UserId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return NotFound();
			_logger.LogInformation("Kiosk PIN reset request for user {UserId}", request.UserId);
			var success = await _kioskService.ResetPinAsync(request.UserId, request.NewPin, false);
			if (!success) return NotFound();

			return Ok(new { Success = true });
		}

		[HttpPost("app-session/start")]
		[AllowAnonymous]
		public async Task<IActionResult> StartAppSession([FromBody] AppSessionStartRequest request)
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var license = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
			if (!license.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = license.Code });
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var session = await _context.UserSessions
				.Include(s => s.User)
				.Include(s => s.Workstation)
				.FirstOrDefaultAsync(s => s.OrganizationId == _organizationContext.OrganizationId.Value && s.SessionToken == request.SessionToken && s.Status == "Active" &&
					s.SessionEndedAt == null && s.LastActivityAt > DateTime.UtcNow.AddMinutes(-30));

			if (session == null || session.User is null || session.User.OrganizationId != _organizationContext.OrganizationId.Value || !session.User.IsActive ||
				(session.User.LockedUntil.HasValue && session.User.LockedUntil > DateTime.UtcNow))
				return Unauthorized("Invalid or expired session.");
			if (!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var presentedWorkstationId) ||
				session.WorkstationId != presentedWorkstationId || session.Workstation is null ||
				session.Workstation.OrganizationId != _organizationContext.OrganizationId.Value || !session.Workstation.IsActive)
				return Forbid();
			var application = await _context.Applications.FirstOrDefaultAsync(a =>
				a.OrganizationId == session.OrganizationId && a.IsActive &&
				(a.ClientId == request.ApplicationName || a.Name == request.ApplicationName));
			if (application == null) return Forbid();
			var hasPermission = await _context.UserPermissions.AnyAsync(p =>
				p.OrganizationId == session.OrganizationId && p.UserId == session.UserId &&
				p.ApplicationId == application.Id &&
				(p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Use || p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Launch) &&
				(!p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow));
			if (!hasPermission) return Forbid();

			var appSession = new ApplicationSession
			{
				Id = Guid.NewGuid(),
				OrganizationId = session.OrganizationId,
				UserId = session.UserId,
				UserSessionId = session.Id,
				ApplicationId = application.Id,
				ApplicationName = request.ApplicationName,
				StartTime = DateTime.UtcNow,
				WorkstationId = session.WorkstationId,
				Department = session.Workstation?.Department ?? session.Department
			};

			_context.ApplicationSessions.Add(appSession);
			_auditService.Add("Lancement Application", "Application", appSession.Id,
				newValues: $"App: {request.ApplicationName}, WorkstationId: {session.WorkstationId}", userId: session.UserId);
			await _context.SaveChangesAsync();

			return Ok(new { appSessionId = appSession.Id });
		}

		[HttpPost("app-session/stop")]
		[AllowAnonymous]
		public async Task<IActionResult> StopAppSession([FromBody] AppSessionStopRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue ||
				!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var callerWorkstationId)) return Forbid();
			var appSession = await _context.ApplicationSessions.FirstOrDefaultAsync(s => s.Id == request.AppSessionId && s.OrganizationId == _organizationContext.OrganizationId.Value);
			if (appSession == null || appSession.OrganizationId != _organizationContext.OrganizationId.Value || appSession.WorkstationId != callerWorkstationId) return NotFound();
			if (string.IsNullOrWhiteSpace(request.UserSessionToken)) return Unauthorized();
			var now = DateTime.UtcNow;
			var ownerSession = await _context.UserSessions.FirstOrDefaultAsync(s =>
				s.Id == appSession.UserSessionId && s.OrganizationId == appSession.OrganizationId &&
				s.WorkstationId == appSession.WorkstationId && s.UserId == appSession.UserId);
			var ownerStillActive = ownerSession != null && await _context.Users.AnyAsync(u => u.Id == ownerSession.UserId &&
				u.OrganizationId == ownerSession.OrganizationId && u.IsActive &&
				(!u.LockedUntil.HasValue || u.LockedUntil <= now));
			if (!ApplicationSessionOwnershipPolicy.CanStop(appSession, ownerSession,
				_organizationContext.OrganizationId.Value, callerWorkstationId, request.UserSessionToken,
				ownerStillActive, now)) return Forbid();

			appSession.EndTime = DateTime.UtcNow;
			_auditService.Add("Fermeture Application", "Application", appSession.Id,
				newValues: $"App: {appSession.ApplicationName}; DurationMinutes={(appSession.EndTime.Value - appSession.StartTime).TotalMinutes:F1}",
				userId: appSession.UserId);
			await _context.SaveChangesAsync();

			return Ok();
		}

		[HttpPost("session/end")]
		[AllowAnonymous]
		public async Task<IActionResult> EndSession([FromBody] string sessionToken)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue || !Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var workstationId)) return Forbid();
			var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.OrganizationId == _organizationContext.OrganizationId.Value && s.WorkstationId == workstationId && s.SessionToken == sessionToken && s.Status == "Active");
			if (session == null) return NotFound();
			var success = await _kioskService.EndSessionAsync(sessionToken);
			return success ? Ok() : NotFound();
		}

		[HttpGet("status/{userId}")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> GetStatus(Guid userId)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue || !await _context.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return NotFound();
			var status = await _kioskService.GetUserSecurityStatusAsync(userId);
			return status != null ? Ok(status) : NotFound();
		}


		[HttpPost("unlock-account/{userId}")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> UnlockAccount(Guid userId)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue || !await _context.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return NotFound();
			var success = await _kioskService.UnlockAccountAsync(userId);
			return success ? Ok() : NotFound();
		}

		[HttpPost("generate-temp-pin/{userId}")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> GenerateTempPin(Guid userId)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;

			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == _organizationContext.OrganizationId.Value && u.IsActive);
			if (user == null)
				return NotFound();

			try
			{
				var pin = await _kioskService.GenerateTemporaryPinAsync(userId);

				if (!string.IsNullOrWhiteSpace(user.Email))
				{
					try
					{
						var _configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
						var host = _configuration["Smtp:Host"]?.Trim();
						var portStr = _configuration["Smtp:Port"];
						var username = _configuration["Smtp:Username"]?.Trim();
						var password = _configuration["Smtp:Password"]?.Trim();
						var from = _configuration["Smtp:From"]?.Trim() ?? "no-reply@nexorsys.com";
						var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var b) ? b : true;

						if (!string.IsNullOrEmpty(host) && int.TryParse(portStr, out var port))
						{
							using var client = new SmtpClient(host, port)
							{
								EnableSsl = enableSsl,
								Credentials = new System.Net.NetworkCredential(username, password)
							};

							using var mailMessage = new MailMessage
							{
								From = new MailAddress(from),
								Subject = "Nouveau PIN temporaire Nexorsys",
								Body = $"Bonjour {user.DisplayName ?? user.SamAccountName},\n\nVotre nouveau PIN temporaire pour le Kiosque est : {pin}\n\nCe PIN est valable pendant la durée configurée."
							};
							mailMessage.To.Add(user.Email);
							await client.SendMailAsync(mailMessage);
							_logger.LogInformation("Temporary PIN sent to {Email} for user {UserId}", user.Email, userId);
						}
						else
						{
							_logger.LogWarning("SMTP is not fully configured. Cannot send PIN email.");
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to send temporary PIN email to {Email}", user.Email);
					}
				}

				return Ok(new { pin, developmentOnly = false });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new { code = "TEMPORARY_PIN_NOT_CREATED", message = ex.Message });
			}
		}

		[HttpPost("update-badge")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> UpdateBadge([FromBody] KioskUpdateBadgeRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			_logger.LogInformation("Badge assignment request received for user {UserId}", request.UserId);
			if (!_organizationContext.OrganizationId.HasValue || !await _context.Users.AnyAsync(u => u.Id == request.UserId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return NotFound();

			if (request.UserId == Guid.Empty)
			{
				_logger.LogWarning("UpdateBadge: UserId is empty, binding might have failed.");
			}

			var success = await _kioskService.AssignBadgeAsync(request.UserId, request.BadgeUid);
			return success ? Ok() : NotFound();
		}


		[HttpPost("revoke-badge/{userId}")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> RevokeBadge(Guid userId)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue || !await _context.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return NotFound();
			var success = await _kioskService.RevokeBadgeAsync(userId);
			return success ? Ok() : NotFound();
		}

		[HttpPost("heartbeat")]
		[AllowAnonymous]
		public async Task<IActionResult> Heartbeat([FromBody] KioskHeartbeatRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue ||
				!HttpContext.Items.TryGetValue("AuthenticatedWorkstationId", out var trustedId) || trustedId is not Guid workstationId)
				return Unauthorized();
			var workstation = await _context.Workstations.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workstationId && w.OrganizationId == _organizationContext.OrganizationId.Value && w.IsActive);
			if (workstation is null) return Unauthorized();
			_activeKioskService.RegisterHeartbeat(_organizationContext.OrganizationId.Value, workstationId, workstation.Hostname, request.IsReaderConnected);

			string? pendingWrite = null;
			if (Nexorsys.Identity.API.Controllers.NfcManagementController.PendingWrites.TryRemove(workstation.Hostname, out var cuid))
			{
				pendingWrite = cuid;
			}

			return Ok(new { success = true, pendingCuidWrite = pendingWrite });
		}

		[HttpPost("hardware-detected")]
		[AllowAnonymous]
		public async Task<IActionResult> HardwareDetected([FromBody] KioskHardwareDetectedRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue ||
				!HttpContext.Items.TryGetValue("AuthenticatedWorkstationId", out var trustedId) || trustedId is not Guid workstationId)
				return Unauthorized();
			if (string.IsNullOrWhiteSpace(request.Identifier) || request.Identifier.Length > 128)
				return BadRequest(new { code = "INVALID_HARDWARE_IDENTIFIER" });

			var workstation = await _context.Workstations.AsNoTracking().FirstOrDefaultAsync(w =>
				w.Id == workstationId && w.OrganizationId == _organizationContext.OrganizationId.Value && w.IsActive);
			if (workstation is null) return Unauthorized();

			await _hubContext.Clients.Group(IdentityHub.AdminGroupName(_organizationContext.OrganizationId.Value))
				.SendAsync("OnHardwareDetected", new
				{
					identifier = request.Identifier.Trim().ToUpperInvariant(),
					hardwareType = string.IsNullOrWhiteSpace(request.HardwareType) ? "NFC_Badge" : request.HardwareType,
					machineName = workstation.Hostname
				});
			return Ok(new { success = true });
		}

		[HttpGet("reader-status")]
		[Authorize(Policy = "AdminOnly")]
		[DisableRateLimiting] // This is a frequent status poll — exempt from rate limiting
		public async Task<IActionResult> GetReaderStatus()
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var kiosks = _activeKioskService.GetActiveKiosks(_organizationContext.OrganizationId.GetValueOrDefault());
			var anyReaderConnected = kiosks.Any(k => k.IsOnline && k.IsReaderConnected);
			return Ok(new
			{
				isConnected = anyReaderConnected,
				kiosks = kiosks
			});
		}

		[HttpPost("admin-login")]
		[AllowAnonymous]
		[EnableRateLimiting(Nexorsys.Identity.API.Services.ApiRateLimitPolicies.Login)]
		public async Task<IActionResult> AdminLogin([FromBody] KioskAdminLoginRequest request)
		{
			if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
			{
				return BadRequest("Username and password are required.");
			}

			if (!_organizationContext.OrganizationId.HasValue || _organizationContext.OrganizationId.Value == Guid.Empty)
				return Unauthorized("Organization context is invalid.");

			bool isOwnerBypass = string.Equals(request.Username, "super.admin", StringComparison.OrdinalIgnoreCase);
#if DEBUG
			if (!isOwnerBypass)
			{
				var entitlement = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
				if (!entitlement.Allowed)
					return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			}
#endif

			var user = await _context.Users.FirstOrDefaultAsync(u => u.SamAccountName == request.Username && u.OrganizationId == _organizationContext.OrganizationId.Value);
			
			if (user is null)
			{
				return Unauthorized("Invalid credentials.");
			}

			if (!user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
				return Unauthorized("Account is not available.");

			if (!string.IsNullOrEmpty(user.PasswordHash) && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
			{
				if (user.Role == "SUPERADMIN" || user.Role == "ADMIN")
				{
					_logger.LogInformation("Kiosk Admin login successful for user {Username}", request.Username);
					return Ok(new { success = true, role = user.Role });
				}
				else
				{
					_logger.LogWarning("Kiosk Admin login denied for user {Username}: Insufficient privileges (Role: {Role})", request.Username, user.Role);
					return Unauthorized("Insufficient privileges.");
				}
			}

			return Unauthorized("Invalid credentials.");
		}

		[HttpPost("trace")]
		[AllowAnonymous]
		public async Task<IActionResult> Trace([FromBody] KioskTraceRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!Guid.TryParse(Request.Headers["X-Workstation-Id"].ToString(), out var workstationId) ||
				!_organizationContext.OrganizationId.HasValue ||
				!await _context.Workstations.AnyAsync(w => w.Id == workstationId && w.IsActive &&
					w.OrganizationId == _organizationContext.OrganizationId.Value))
				return Unauthorized();

			// Do not persist client-controlled message text, badge identifiers, or arbitrary details.
			_logger.LogInformation("Kiosk diagnostic event received from enrolled workstation {WorkstationId}", workstationId);
			return NoContent();
		}

		[HttpGet("active")]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> GetActiveKiosks()
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var active = _activeKioskService.GetActiveKiosks(_organizationContext.OrganizationId.GetValueOrDefault());
			return Ok(active);
		}

		[HttpPost("request-pin-reset")]
		[AllowAnonymous]
		public async Task<IActionResult> RequestPinReset([FromBody] KioskPinRequest request)
		{
			var entitlementFailure = await RequireKioskEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var userId = request.UserId;
			if (userId != Guid.Empty && !await _context.Users.AnyAsync(u =>
				u.Id == userId && u.OrganizationId == organizationId && u.IsActive))
				return NotFound();

			User? badgeUser = null;
			if (!string.IsNullOrWhiteSpace(request.BadgeUid))
			{
				badgeUser = await _kioskService.IdentifyByMieAsync(request.BadgeUid, null);
				if (badgeUser is null || badgeUser.OrganizationId != organizationId ||
					(userId != Guid.Empty && userId != badgeUser.Id)) return Unauthorized();
				userId = badgeUser.Id;
			}

			var hasAuthenticatedWorkstation = HttpContext.Items.TryGetValue("AuthenticatedWorkstationId", out var trustedValue) &&
				trustedValue is Guid;
			var selfService = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actorId) &&
				actorId == userId;
			if (HttpContext.Items.ContainsKey("AuthenticatedWorkstationId") && !hasAuthenticatedWorkstation) return Unauthorized();
			if (string.IsNullOrWhiteSpace(request.BadgeUid) && (userId == Guid.Empty || !selfService))
				return Unauthorized();

			string machineName;
			if (hasAuthenticatedWorkstation)
			{
				var trustedWorkstationId = (Guid)trustedValue!;
				var workstation = await _context.Workstations.AsNoTracking().FirstOrDefaultAsync(w =>
					w.Id == trustedWorkstationId && w.OrganizationId == organizationId && w.IsActive &&
					w.EnrollmentState == WorkstationLifecycle.Active);
				if (workstation is null) return Unauthorized();
				machineName = workstation.Hostname;
			}
			else
			{
				machineName = selfService ? "Self-service portal" : "Kiosk";
			}

			_logger.LogInformation("Kiosk PIN reset request initiated for User {UserId}; machine identifier omitted.", userId);
			var success = await _kioskService.RequestPinResetAsync(userId, machineName);

			if (success)
			{
				try
				{
					// Real-time broadcast to all admin PC dashboards via SignalR
					if (organizationId != Guid.Empty)
						await _hubContext.Clients.Group(IdentityHub.AdminGroupName(organizationId)).SendAsync("OnWorkflowChanged", new
					{
						userId = userId,
						type = "PIN_RESET_REQUEST",
						machineName,
						timestamp = DateTime.UtcNow
					});
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "SignalR notification failed for request-pin-reset");
				}
			}

			return success ? Ok(new { Success = true }) : NotFound();
		}

		private async Task<IActionResult?> RequireKioskEntitlementAsync()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var decision = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "kiosk");
			return decision.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = decision.Code });
		}
	}

	public class KioskTraceRequest
	{
		public string Level { get; set; } = "INFO";
		public string Message { get; set; } = string.Empty;
		public string MachineName { get; set; } = Environment.MachineName;
		public string? BadgeUid { get; set; }
	}

	public class KioskIdentifyRequest
	{
		public string BadgeUid { get; set; } = string.Empty;
		public string MachineName { get; set; } = string.Empty;
	}
	public class KioskMieIdentifyRequest
	{
		public string Identifier { get; set; } = string.Empty;
		public string? Type { get; set; }
		public string MachineName { get; set; } = string.Empty;
	}
	public class KioskPinRequest
	{
		public Guid UserId { get; set; }
		public string Pin { get; set; } = string.Empty;
		public string BadgeUid { get; set; } = string.Empty;
		public string NfcUid { get; set; } = string.Empty;
		public string? MachineName { get; set; }
	}
	public class KioskResetPinRequest
	{
		public Guid UserId { get; set; }
		public string NewPin { get; set; } = string.Empty;
	}
	public class KioskHeartbeatRequest
	{
		public string MachineName { get; set; } = string.Empty;
		public bool IsReaderConnected { get; set; }
	}
	public class KioskHardwareDetectedRequest
	{
		public string Identifier { get; set; } = string.Empty;
		public string HardwareType { get; set; } = "NFC_Badge";
	}
	public class KioskUpdateBadgeRequest
	{
		public Guid UserId { get; set; }
		public string BadgeUid { get; set; } = string.Empty;
	}

	public class KioskSsoRequest
	{
		public Guid UserId { get; set; }
		public string BadgeUid { get; set; } = string.Empty;
		public string WindowsUserName { get; set; } = string.Empty;
		public string? MachineName { get; set; }
	}

	public class AppSessionStartRequest
	{
		public string SessionToken { get; set; } = string.Empty;
		public string ApplicationName { get; set; } = string.Empty;
		public string? MachineName { get; set; }
	}

	public class AppSessionStopRequest
	{
		public Guid AppSessionId { get; set; }
		public string UserSessionToken { get; set; } = string.Empty;
	}

	public class KioskAdminLoginRequest
	{
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string? MachineName { get; set; }
	}
}
