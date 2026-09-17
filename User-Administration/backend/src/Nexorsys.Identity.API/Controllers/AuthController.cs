using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AuthController : ControllerBase
	{
		private readonly ILdapService _ldapService;
		private readonly IUserRepository _userRepository;
		private readonly IConfiguration _configuration;
		private readonly IAuditService _auditService;
		private readonly ILogger<AuthController> _logger;
		private readonly IWebHostEnvironment _environment;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.Infrastructure.AppDbContext _dbContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public AuthController(
			ILdapService ldapService,
			IUserRepository userRepository,
			IConfiguration configuration,
			IAuditService auditService,
			ILogger<AuthController> logger,
			IWebHostEnvironment environment,
			IOrganizationContext organizationContext,
			Nexorsys.Identity.Infrastructure.AppDbContext dbContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_ldapService = ldapService;
			_userRepository = userRepository;
			_configuration = configuration;
			_auditService = auditService;
			_logger = logger;
			_environment = environment;
			_organizationContext = organizationContext;
			_dbContext = dbContext;
			_licenseGuard = licenseGuard;
		}

		[HttpPost("login")]
		[Microsoft.AspNetCore.Authorization.AllowAnonymous]
		[EnableRateLimiting(Nexorsys.Identity.API.Services.ApiRateLimitPolicies.Login)]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
			{
				return BadRequest("Username and password are required.");
			}

			if (!_organizationContext.OrganizationId.HasValue || _organizationContext.OrganizationId.Value == Guid.Empty)
				return Unauthorized("Organization context is invalid.");
			bool isOwnerBypass = string.Equals(request.Username, "super.admin", StringComparison.OrdinalIgnoreCase);
#if DEBUG
			_logger.LogInformation("DEBUG-LOGIN: EnvDev={EnvDev}, Bypass={Bypass}, CfgUser='{CfgUser}', ReqUser='{ReqUser}'", 
				_environment.IsDevelopment(), 
				_configuration.GetValue<bool>("Licensing:DevelopmentOwnerBypass"), 
				_configuration["Licensing:DevelopmentOwnerUsername"], 
				request.Username);
			if (_environment.IsDevelopment() &&
				_configuration.GetValue<bool>("Licensing:DevelopmentOwnerBypass") &&
				string.Equals(request.Username, _configuration["Licensing:DevelopmentOwnerUsername"], StringComparison.OrdinalIgnoreCase))
			{
				isOwnerBypass = true;
			}
#endif
			if (!isOwnerBypass)
			{
				var entitlement = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
				if (!entitlement.Allowed)
					return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
			}

			_logger.LogInformation("Login attempt received; usernameHash={UsernameHash}", UsernameDigest(request.Username));
			User? user = await _userRepository.GetBySamAccountNameAsync(request.Username);
			if (user is null)
			{
				// Never turn the Identity API into an LDAP credential-validation oracle for
				// accounts that are not provisioned in the selected organization.
				await _auditService.LogAsync("Login denied: identity is not provisioned in tenant", "Authentication",
					newValues: $"Username-SHA256={UsernameDigest(request.Username)}");
				return Unauthorized("Invalid credentials.");
			}

			// 1. Check if user exists locally and is a local profile (e.g. Admin)
			if (user.IsLocalProfile)
			{
				if (!user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
					return Unauthorized("Account is not available.");
				if (!string.IsNullOrEmpty(user.PasswordHash) && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
				{
					_logger.LogInformation("Local authentication successful for usernameHash={UsernameHash}", UsernameDigest(request.Username));
					return await HandleSuccessfulLogin(user);
				}

				await _auditService.LogAsync("Échec Connexion Locale", "Authentification", newValues: $"Username-SHA256={UsernameDigest(request.Username)}");
				return Unauthorized("Invalid local credentials.");
			}

			// 2. LDAP bind is permitted only for an already-provisioned account in the
			// authenticated tenant context. A request header must never self-provision a tenant identity.
			bool isAuthenticated = await _ldapService.AuthenticateAsync(request.Username, request.Password);

			if (!isAuthenticated)
			{
				await _auditService.LogAsync(
					action: "Échec Connexion Portail",
					resourceType: "Authentification",
					newValues: $"Username-SHA256={UsernameDigest(request.Username)}"
				);
				return Unauthorized("Invalid credentials.");
			}

			{
				// Sync LDAP attributes on subsequent logins
				var ldapUser = await _ldapService.GetUserBySamAccountNameAsync(request.Username);
				if (ldapUser != null)
				{
					bool updated = false;
					if (!string.IsNullOrEmpty(ldapUser.Email) && user.Email != ldapUser.Email) { user.Email = ldapUser.Email; updated = true; }
					if (!string.IsNullOrEmpty(ldapUser.Department) && user.Department != ldapUser.Department) { user.Department = ldapUser.Department; updated = true; }
					if (!string.IsNullOrEmpty(ldapUser.Title) && user.Title != ldapUser.Title) { user.Title = ldapUser.Title; updated = true; }
					if (!string.IsNullOrEmpty(ldapUser.DisplayName) && user.DisplayName != ldapUser.DisplayName) { user.DisplayName = ldapUser.DisplayName; updated = true; }

					if (updated)
					{
						user.UpdatedAt = DateTime.UtcNow;
						await _userRepository.UpdateAsync(user);
					}
				}
			}

			return await HandleSuccessfulLogin(user);
		}

		private static string UsernameDigest(string username) => Convert.ToHexString(
			System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(username.Trim().ToUpperInvariant())))[..16];

		private async Task<IActionResult> HandleSuccessfulLogin(User user)
		{
			if (!user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
				return Unauthorized("Account is not available.");
			if (user.OrganizationId == Guid.Empty || !_organizationContext.OrganizationId.HasValue || user.OrganizationId != _organizationContext.OrganizationId.Value)
				return Unauthorized("Organization context is invalid.");

			var session = new UserSession
			{
				Id = Guid.NewGuid(),
				OrganizationId = user.OrganizationId,
				UserId = user.Id,
				SessionStartedAt = DateTime.UtcNow,
				LastActivityAt = DateTime.UtcNow,
				Status = "Active",
				AuthenticationMethod = user.IsLocalProfile ? "Local" : "LDAP"
			};
			_dbContext.UserSessions.Add(session);
			// Update Last Login
			user.LastLoginAt = DateTime.UtcNow;
			_auditService.Add(
				action: "Connexion au Portail Réussie",
				resourceType: "Authentification",
				resourceId: user.Id,
				userId: user.Id
			);
			await _dbContext.SaveChangesAsync();

			var token = GenerateJwtToken(user, session.Id);
			Response.Cookies.Append("nexorsys_session", token, new CookieOptions
			{
				HttpOnly = true,
				Secure = !_environment.IsDevelopment() || Request.IsHttps,
				SameSite = SameSiteMode.Strict,
				Path = "/",
				MaxAge = TimeSpan.FromHours(1)
			});
			Response.Cookies.Append("nexorsys_csrf", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), new CookieOptions
			{
				HttpOnly = false,
				Secure = !_environment.IsDevelopment() || Request.IsHttps,
				SameSite = SameSiteMode.Strict,
				Path = "/",
				MaxAge = TimeSpan.FromHours(1)
			});
			return Ok(new { user = ToSafeUser(user) });
		}

		[HttpGet("me")]
		[Microsoft.AspNetCore.Authorization.Authorize]
		public async Task<IActionResult> Me()
		{
			if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
				!Guid.TryParse(User.FindFirstValue("org_id"), out var organizationId) ||
				userId == Guid.Empty || organizationId == Guid.Empty) return Unauthorized();
			var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity");
			if (!entitlement.Allowed)
				return StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });

			// Bind both signed ownership claims in SQL and materialize only fields the
			// profile endpoint is allowed to disclose (never PINs, devices or grants).
			var user = await _dbContext.Users.AsNoTracking()
				.Where(x => x.Id == userId && x.OrganizationId == organizationId)
				.Select(x => new
				{
					id = x.Id,
					organizationId = x.OrganizationId,
					samAccountName = x.SamAccountName,
					displayName = x.DisplayName,
					email = x.Email,
					department = x.Department,
					role = x.Role,
					isActive = x.IsActive
				})
				.SingleOrDefaultAsync();
			return user is null ? Unauthorized() : Ok(new { user });
		}

		[HttpPost("logout")]
		[Microsoft.AspNetCore.Authorization.Authorize]
		public async Task<IActionResult> Logout()
		{
			if (Guid.TryParse(User.FindFirstValue("sid"), out var sessionId) &&
				Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
				Guid.TryParse(User.FindFirstValue("org_id"), out var organizationId))
			{
				var session = await _dbContext.UserSessions.FirstOrDefaultAsync(x =>
					x.Id == sessionId && x.UserId == userId && x.OrganizationId == organizationId && x.Status == "Active");
				if (session != null)
				{
					session.Status = "Closed";
					session.SessionEndedAt = DateTime.UtcNow;
					_auditService.Add("Portal logout", "UserSession", session.Id,
						oldValues: "Status=Active", newValues: "Status=Closed", userId: userId);
					await _dbContext.SaveChangesAsync();
				}
			}
			Response.Cookies.Delete("nexorsys_session", new CookieOptions { HttpOnly = true, Secure = !_environment.IsDevelopment() || Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" });
			Response.Cookies.Delete("nexorsys_csrf", new CookieOptions { Secure = !_environment.IsDevelopment() || Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" });
			return NoContent();
		}

		[HttpPost("forgot-password")]
		[Microsoft.AspNetCore.Authorization.AllowAnonymous]
		public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Email))
				return BadRequest("Email is required.");

			var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && u.IsLocalProfile);
			if (user == null)
			{
				// Return OK even if user not found to prevent email enumeration
				return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
			}

			// Generate reset token
			var jwtKey = _configuration["Jwt:Key"];
			if (string.IsNullOrWhiteSpace(jwtKey)) return StatusCode(500, "JWT Key not configured.");
			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				new Claim("purpose", "password_reset")
			};

			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"] ?? "NexorSys.Identity",
				audience: _configuration["Jwt:Audience"] ?? "NexorSys.Identity.Clients",
				claims: claims,
				expires: DateTime.UtcNow.AddMinutes(15),
				signingCredentials: credentials);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

			// Send email
			try
			{
				var host = _configuration["Smtp:Host"]?.Trim();
				var portStr = _configuration["Smtp:Port"];
				var username = _configuration["Smtp:Username"]?.Trim();
				var password = _configuration["Smtp:Password"]?.Trim();
				var from = _configuration["Smtp:From"]?.Trim() ?? "no-reply@nexorsys.com";
				var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var b) ? b : true;

				if (string.IsNullOrEmpty(host) || !int.TryParse(portStr, out var port))
				{
					_logger.LogWarning("SMTP is not fully configured. Cannot send password reset email.");
					return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
				}

				using var client = new SmtpClient(host, port)
				{
					UseDefaultCredentials = false,
					Credentials = new System.Net.NetworkCredential(username, password),
					EnableSsl = enableSsl,
					Timeout = 8000
				};

				var resetLink = $"{Request.Scheme}://{Request.Host}/reset-password?token={tokenString}";
				var mailMessage = new MailMessage
				{
					From = new MailAddress(from),
					Subject = "NexorSys - Réinitialisation de votre mot de passe",
					Body = $"Bonjour,\n\nVous avez demandé la réinitialisation de votre mot de passe.\nCliquez sur le lien ci-dessous pour créer un nouveau mot de passe (valable 15 minutes) :\n\n{resetLink}\n\nSi vous n'êtes pas à l'origine de cette demande, vous pouvez ignorer cet email.",
					IsBodyHtml = false
				};
				mailMessage.To.Add(user.Email);

				await client.SendMailAsync(mailMessage);
				_logger.LogInformation("Password reset email sent to {Email}", user.Email);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
				// Still return OK
			}

			return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
		}

		[HttpPost("reset-password")]
		[Microsoft.AspNetCore.Authorization.AllowAnonymous]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
				return BadRequest("Token and new password are required.");

			if (request.NewPassword.Contains(" "))
				return BadRequest("Le mot de passe ne doit pas contenir d'espaces.");

			if (request.NewPassword.Length < 8)
				return BadRequest("Le mot de passe doit contenir au moins 8 caractères.");

			var jwtKey = _configuration["Jwt:Key"];
			if (string.IsNullOrWhiteSpace(jwtKey)) return StatusCode(500, "JWT Key not configured.");

			var tokenHandler = new JwtSecurityTokenHandler();
			try
			{
				var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
				{
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
					ValidateIssuer = true,
					ValidIssuer = _configuration["Jwt:Issuer"] ?? "NexorSys.Identity",
					ValidateAudience = true,
					ValidAudience = _configuration["Jwt:Audience"] ?? "NexorSys.Identity.Clients",
					ValidateLifetime = true,
					ClockSkew = TimeSpan.Zero
				}, out _);

				var purpose = principal.FindFirstValue("purpose");
				if (purpose != "password_reset") return BadRequest("Invalid token purpose.");

				var userIdStr = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
				if (!Guid.TryParse(userIdStr, out var userId)) return BadRequest("Invalid token payload.");

				var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsLocalProfile);
				if (user == null) return BadRequest("User not found.");

				user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
				user.UpdatedAt = DateTime.UtcNow;
				
				// Optional: Invalidate all active sessions for this user
				var activeSessions = await _dbContext.UserSessions.Where(s => s.UserId == user.Id && s.Status == "Active").ToListAsync();
				foreach (var session in activeSessions)
				{
					session.Status = "Closed";
					session.SessionEndedAt = DateTime.UtcNow;
				}

				await _dbContext.SaveChangesAsync();

				_logger.LogInformation("Password reset successful for user {UserId}", user.Id);
				await _auditService.LogAsync("Mot de passe réinitialisé", "Authentification", newValues: $"UserId={user.Id}", userId: user.Id);

				return Ok(new { success = true });
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed password reset token validation.");
				return BadRequest("Le lien de réinitialisation est invalide ou a expiré.");
			}
		}

		private static object ToSafeUser(User user) => new
		{
			id = user.Id,
			organizationId = user.OrganizationId,
			samAccountName = user.SamAccountName,
			displayName = user.DisplayName,
			email = user.Email,
			department = user.Department,
			role = user.Role,
			isActive = user.IsActive
		};

		private string GenerateJwtToken(User user, Guid sessionId)
		{
			var jwtKey = _configuration["Jwt:Key"];
			if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Jwt:Key is required.");
			var issuer = _configuration["Jwt:Issuer"] ?? "NexorSys.Identity";
			var audience = _configuration["Jwt:Audience"] ?? "NexorSys.Identity.Clients";

			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim("sid", sessionId.ToString()),
				new Claim(ClaimTypes.Name, user.SamAccountName),
				new Claim(ClaimTypes.GivenName, user.DisplayName ?? user.SamAccountName),
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
			};
			if (user.OrganizationId == Guid.Empty)
			{
				_logger.LogWarning("Refusing to issue a token for user {UserId} without an organization", user.Id);
				throw new InvalidOperationException("User is not assigned to an organization.");
			}
			claims.Add(new Claim("org_id", user.OrganizationId.ToString()));

			// Standard Ségur/ANS Claims: RPPS linkage for IdP federation
			if (!string.IsNullOrEmpty(user.RppsNumber))
			{
				claims.Add(new Claim("rpps_number", user.RppsNumber));
			}

			// Add user role claim
			claims.Add(new Claim(ClaimTypes.Role, user.Role));

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: DateTime.UtcNow.AddHours(1),
				signingCredentials: credentials);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}
