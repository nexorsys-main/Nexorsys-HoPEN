using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Infrastructure.Repositories;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.SignalR;
using Nexorsys.Identity.API.Hubs;
using Npgsql;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class UsersController : ControllerBase
	{
		private readonly ILdapService _ldapService;
		private readonly IUserRepository _userRepository;
		private readonly IAuditService _auditService;
		private readonly ILogger<UsersController> _logger;
		private readonly AppDbContext _context;
		private readonly IHubContext<IdentityHub> _hubContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public UsersController(
			ILdapService ldapService,
			IUserRepository userRepository,
			IAuditService auditService,
			ILogger<UsersController> logger,
			AppDbContext context,
			IHubContext<IdentityHub> hubContext,
			IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_ldapService = ldapService;
			_userRepository = userRepository;
			_auditService = auditService;
			_logger = logger;
			_context = context;
			_hubContext = hubContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet("me")]
		public async Task<IActionResult> GetMyProfile()
		{
			var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (!Guid.TryParse(userIdStr, out var userId))
			{
				return Unauthorized();
			}

			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var organizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			var user = await _context.Users.AsNoTracking().Include(u => u.UserPermissions!.Where(p => p.OrganizationId == organizationId)).ThenInclude(p => p.Application)
				.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null)
			{
				return NotFound();
			}

			return Ok(ToSafeUser(user, organizationId));
		}

		[HttpPatch("me")]
		public async Task<IActionResult> UpdateMyProfile([FromBody] UserProfileUpdateDto updateDto)
		{
			var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			var organizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			var user = await _context.Users.Include(u => u.UserPermissions!.Where(p => p.OrganizationId == organizationId)).ThenInclude(p => p.Application)
				.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null) return NotFound();

			var changedFields = new List<string>();
			if (!string.IsNullOrEmpty(updateDto.Email)) { user.Email = updateDto.Email; changedFields.Add("Email"); }
			if (!string.IsNullOrEmpty(updateDto.PhoneNumber)) { user.PhoneNumber = updateDto.PhoneNumber; changedFields.Add("PhoneNumber"); }

			if (!string.IsNullOrEmpty(updateDto.NewPassword))
			{
				if (updateDto.NewPassword.Contains(" "))
					return BadRequest("Le mot de passe ne doit pas contenir d'espaces.");
				user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateDto.NewPassword);
				changedFields.Add("Password");
			}

			user.UpdatedAt = DateTime.UtcNow;
			var actorId = Guid.TryParse(userIdStr, out var parsedActor) ? parsedActor : (Guid?)null;
			_context.AuditLogs.Add(CreateUserAudit(user.OrganizationId, actorId,
				"Mise à jour du profil personnel", user.Id, null,
				$"ChangedFields={string.Join(',', changedFields)}; PasswordChanged={!string.IsNullOrEmpty(updateDto.NewPassword)}"));
			await _userRepository.UpdateAsync(user);

			return Ok(ToSafeUser(user, organizationId));
		}

		public class UserProfileUpdateDto
		{
			public string? Email { get; set; }
			public string? PhoneNumber { get; set; }
			public string? NewPassword { get; set; }
		}

		[HttpGet("search")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> Search(string? query, string? department)
		{
			try
			{
				var entitlementFailure = await RequireIdentityEntitlementAsync();
				if (entitlementFailure is not null) return entitlementFailure;
				if (!_organizationContext.OrganizationId.HasValue) return Forbid();
				var localUsers = _context.Users.Where(u => u.OrganizationId == _organizationContext.OrganizationId.Value);
				if (!string.IsNullOrWhiteSpace(query)) localUsers = localUsers.Where(u => u.SamAccountName.Contains(query) || (u.DisplayName != null && u.DisplayName.Contains(query)) || (u.Email != null && u.Email.Contains(query)));
				if (!string.IsNullOrWhiteSpace(department)) localUsers = localUsers.Where(u => u.Department != null && u.Department.Contains(department));
				return Ok((await localUsers.AsNoTracking().OrderBy(u => u.LastName).Take(50).ToListAsync())
					.Select(user => ToSafeUser(user, _organizationContext.OrganizationId.Value)));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error searching users; query and department omitted from logs.");
				return StatusCode(500, "Internal server error.");
			}
		}

		[HttpGet("{samAccountName}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetBySamAccountName(string samAccountName)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var user = await _context.Users.AsNoTracking().Include(u => u.UserPermissions!.Where(p => p.OrganizationId == _organizationContext.OrganizationId.Value)).ThenInclude(p => p.Application)
				.FirstOrDefaultAsync(u => u.OrganizationId == _organizationContext.OrganizationId.Value && u.SamAccountName == samAccountName);
			if (user == null) return NotFound();
			return Ok(ToSafeUser(user, _organizationContext.OrganizationId.Value));
		}

		[HttpGet("id/{id:guid}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetById(Guid id)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var user = await _context.Users.AsNoTracking().Include(u => u.UserPermissions!.Where(p => p.OrganizationId == _organizationContext.OrganizationId.Value)).ThenInclude(p => p.Application)
				.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == _organizationContext.OrganizationId.Value);
			if (user == null) return NotFound();
			return Ok(ToSafeUser(user, _organizationContext.OrganizationId.Value));
		}

		[HttpPost("sync/all")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> SyncAllFromAd()
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			try
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
				var organizationId = _organizationContext.OrganizationId.Value;
				var adUsers = await _ldapService.SearchUsersAsync(""); // Get all users
				var syncedCount = 0;

				foreach (var adUser in adUsers)
				{
					if (string.IsNullOrEmpty(adUser.SamAccountName)) continue;
					adUser.OrganizationId = organizationId;
					var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.SamAccountName == adUser.SamAccountName);
					if (existingUser != null)
					{
						existingUser.DisplayName = adUser.DisplayName;
						existingUser.Email = adUser.Email;
						existingUser.FirstName = adUser.FirstName;
						existingUser.LastName = adUser.LastName;
						existingUser.UpdatedAt = DateTime.UtcNow;
						await _userRepository.UpdateAsync(existingUser);
					}
					else
					{
						var identityFeature = await _licenseGuard.HasFeatureAsync(organizationId, "identity");
						if (!identityFeature.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = identityFeature.Code });
						var entitlement = await _licenseGuard.CanAddUserAsync(organizationId);
						if (!entitlement.Allowed) return Conflict(new { code = entitlement.Code });
						adUser.Id = Guid.NewGuid();
						adUser.Role = "VIEWER";
						adUser.IsActive = true;
						adUser.CreatedAt = DateTime.UtcNow;
						adUser.UpdatedAt = DateTime.UtcNow;
						await _userRepository.AddAsync(adUser);
					}
					syncedCount++;
				}

				await _auditService.LogAsync(
					action: "Synchronisation globale depuis l'AD",
					resourceType: "System",
					resourceId: Guid.Empty,
					newValues: $"{syncedCount} utilisateurs synchronisés."
				);
				await transaction.CommitAsync();

				return Ok(new { Message = $"{syncedCount} utilisateurs synchronisés." });
			}
			catch (Exception ex) when (Nexorsys.Identity.API.Services.DatabaseConcurrencyErrors.IsSerializationFailure(ex))
			{
				_logger.LogInformation(ex, "Directory synchronization was rejected after a concurrent database serialization conflict.");
				return Conflict(new { code = "CONCURRENT_WRITE_RETRY" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Directory synchronization failed.");
				return StatusCode(503, "Directory synchronization is unavailable.");
			}
		}

		[HttpPost("sync/{samAccountName}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> SyncFromAd(string samAccountName)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			User? adUser = null;
			try
			{
				adUser = await _ldapService.GetUserBySamAccountNameAsync(samAccountName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Directory lookup failed during explicit synchronization.");
				return StatusCode(503, "Directory synchronization is unavailable.");
			}

			if (adUser == null) return NotFound($"Utilisateur '{samAccountName}' non trouvé dans l'Active Directory.");

			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.OrganizationId == _organizationContext.OrganizationId.Value && u.SamAccountName == samAccountName);
			if (existingUser != null)
			{
				// Update
				existingUser.DisplayName = adUser.DisplayName;
				existingUser.Email = adUser.Email;
				existingUser.FirstName = adUser.FirstName;
				existingUser.LastName = adUser.LastName;
				existingUser.Department = adUser.Department;
				existingUser.Title = adUser.Title;
				existingUser.UpdatedAt = DateTime.UtcNow;
				var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActor) ? parsedActor : (Guid?)null;
				_context.AuditLogs.Add(CreateUserAudit(existingUser.OrganizationId, actorId,
					"Synchronisation de l'utilisateur depuis l'AD", existingUser.Id, null,
					"Source=ActiveDirectory; UpdatedFields=DisplayName,Email,FirstName,LastName,Department,Title"));
				await _userRepository.UpdateAsync(existingUser);

				return Ok(ToSafeUser(existingUser, existingUser.OrganizationId));
			}
			else
			{
				try
				{
				// Create
				await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
				adUser.OrganizationId = _organizationContext.OrganizationId.Value;
				var identityFeature = await _licenseGuard.HasFeatureAsync(adUser.OrganizationId, "identity");
				if (!identityFeature.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = identityFeature.Code });
				var entitlement = await _licenseGuard.CanAddUserAsync(adUser.OrganizationId);
				if (!entitlement.Allowed) return Conflict(new { code = entitlement.Code });
				adUser.Id = adUser.Id == Guid.Empty ? Guid.NewGuid() : adUser.Id;
				adUser.Role = "VIEWER";
				adUser.IsActive = true;
				adUser.CreatedAt = DateTime.UtcNow;
				adUser.UpdatedAt = DateTime.UtcNow;
				var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActor) ? parsedActor : (Guid?)null;
				_context.AuditLogs.Add(CreateUserAudit(adUser.OrganizationId, actorId,
					"Importation de l'utilisateur depuis l'AD", adUser.Id, null, "Source=ActiveDirectory; UserCreated=true"));
				await _userRepository.AddAsync(adUser);
				await transaction.CommitAsync();

				return CreatedAtAction(nameof(GetBySamAccountName), new { samAccountName = adUser.SamAccountName }, ToSafeUser(adUser, adUser.OrganizationId));
			}
			catch (Exception ex) when (Nexorsys.Identity.API.Services.DatabaseConcurrencyErrors.IsSerializationFailure(ex))
			{
				_logger.LogInformation(ex, "Directory user creation was rejected after a concurrent database serialization conflict.");
				return Conflict(new { code = "CONCURRENT_WRITE_RETRY" });
			}
		}
		}

		[HttpGet]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetAll(int page = 1, int pageSize = 20)
		{
			try
			{
				var entitlementFailure = await RequireIdentityEntitlementAsync();
				if (entitlementFailure is not null) return entitlementFailure;
				if (!_organizationContext.OrganizationId.HasValue) return Forbid();
				if (page < 1 || pageSize is < 1 or > 200) return BadRequest("Invalid pagination.");
				var offset = ((long)page - 1) * pageSize;
				if (offset > int.MaxValue) return BadRequest("Invalid pagination.");
				var users = await _context.Users.AsNoTracking().Where(u => u.OrganizationId == _organizationContext.OrganizationId.Value)
					.OrderBy(u => u.LastName).Skip((int)offset).Take(pageSize).ToListAsync();
				return Ok(users.Select(user => ToSafeUser(user, _organizationContext.OrganizationId.Value)));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting all users (Page: {Page}, Size: {Size})", page, pageSize);
				return StatusCode(500, "Internal server error.");
			}
		}

		public class UserCreateDto : User
		{
			public string? MieType { get; set; }
			public List<string>? AuthorizedApps { get; set; }
			public string? TempPin { get; set; }
		}

		[HttpPost]
		[Authorize(Policy = "AdminOnly")]
		public async Task<IActionResult> CreateLocalUser([FromBody] UserCreateDto userDto)
		{
			_logger.LogInformation("CreateLocalUser called; samAccountNameHash={SamAccountNameHash}", IdentifierDigest(userDto?.SamAccountName));

			if (userDto == null)
			{
				_logger.LogWarning("CreateLocalUser: received null user object.");
				return BadRequest("User data is required.");
			}
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;

			if (string.IsNullOrEmpty(userDto.SamAccountName))
				return BadRequest("SamAccountName is required");

			if (userDto.SamAccountName.Contains(" "))
				return BadRequest("Le nom d'utilisateur ne doit pas contenir d'espaces.");

			if (!string.IsNullOrEmpty(userDto.PasswordHash) && userDto.PasswordHash.Contains(" "))
				return BadRequest("Le mot de passe ne doit pas contenir d'espaces.");

			if (!_organizationContext.OrganizationId.HasValue)
				return BadRequest("An organization context is required to create a user.");
			var actorId = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(
				_context, User, _organizationContext.OrganizationId.Value);

			var existing = await _userRepository.GetBySamAccountNameAsync(userDto.SamAccountName);
			if (existing != null)
				return Conflict("User already exists");

			var user = new User
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organizationContext.OrganizationId.Value,
				SamAccountName = userDto.SamAccountName,
				FirstName = userDto.FirstName,
				LastName = userDto.LastName,
				Email = userDto.Email,
				Department = userDto.Department,
				Title = userDto.Title,
				Role = "VIEWER",
				BadgeUid = userDto.BadgeUid,
				CpsId = userDto.CpsId != null && userDto.CpsId.StartsWith("CSP-", StringComparison.OrdinalIgnoreCase) ? "CPS-" + userDto.CpsId.Substring(4) : userDto.CpsId,
				FidoId = userDto.FidoId,
				BadgeType = userDto.MieType ?? "NFC_Badge",
				RppsNumber = userDto.RppsNumber,
				IsLocalProfile = true,
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow,
				UserPermissions = new List<UserPermission>()
			};
			if (!string.IsNullOrWhiteSpace(userDto.Role) &&
				string.Equals(User.FindFirstValue(ClaimTypes.Role), "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
			{
				var requestedRole = userDto.Role.Trim().ToUpperInvariant();
				if (new[] { "VIEWER", "USER", "HR", "DIRECTOR", "ADMIN", "ADMIN_DSI", "SUPERADMIN" }.Contains(requestedRole))
					user.Role = requestedRole;
			}

			user.DisplayName = userDto.DisplayName;
			if (string.IsNullOrEmpty(user.DisplayName))
			{
				user.DisplayName = $"{user.FirstName} {user.LastName}".Trim();
				if (string.IsNullOrEmpty(user.DisplayName)) user.DisplayName = user.SamAccountName;
			}

			if (!string.IsNullOrEmpty(userDto.PasswordHash))
			{
				user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.PasswordHash);
			}

			// Sync Permissions
			if (userDto.AuthorizedApps != null)
			{
				var dbContext = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
				var allApps = await dbContext.Applications.Where(a => a.OrganizationId == _organizationContext.OrganizationId.Value).ToListAsync();

				foreach (var appId in userDto.AuthorizedApps)
				{
					var app = allApps.FirstOrDefault(a => a.ClientId.Equals(appId, StringComparison.OrdinalIgnoreCase));
					if (app != null)
					{
						user.UserPermissions.Add(new UserPermission
						{
							OrganizationId = _organizationContext.OrganizationId.Value,
							Id = Guid.NewGuid(),
							UserId = user.Id,
							ApplicationId = app.Id,
							PermissionLevel = "read",
							GrantedAt = DateTime.UtcNow,
							GrantedBy = actorId
						});
					}
				}
			}

			try
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
				var entitlement = await _licenseGuard.CanAddUserAsync(_organizationContext.OrganizationId.Value);
				var identityFeature = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
				if (!identityFeature.Allowed) return StatusCode(StatusCodes.Status403Forbidden, new { code = identityFeature.Code });
				if (!entitlement.Allowed) return Conflict(new { code = entitlement.Code });
			foreach (var credential in new[] { user.BadgeUid, user.CpsId, user.FidoId }.Where(value => !string.IsNullOrWhiteSpace(value)))
			{
				if (await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context,
					_organizationContext.OrganizationId.Value, user.Id, credential))
					return Conflict(new { code = "CREDENTIAL_ALREADY_OWNED" });
			}

				string? activeBadgeUid = null;
				if (!string.IsNullOrEmpty(userDto.TempPin))
				{
					activeBadgeUid = !string.IsNullOrEmpty(user.CpsId) ? user.CpsId : (!string.IsNullOrEmpty(user.BadgeUid) ? user.BadgeUid : "TEMP");
					activeBadgeUid = activeBadgeUid.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();

					// BadgeUid has a global unique index. Never delete/reassign a row discovered
					// through a privileged context that can see other organizations.
					if (await _context.UserPins.IgnoreQueryFilters().AsNoTracking()
						.AnyAsync(p => p.BadgeUid == activeBadgeUid))
						return Conflict("Credential is already registered.");
				}

				// Keep the user, MIE devices, PIN and audit in one unit of work.
				// UserRepository.AddAsync persists immediately for legacy callers, which
				// would otherwise leave a partially committed local-user mutation here.
				await _context.Users.AddAsync(user);

				// Sync MIE devices AFTER user is saved to avoid FK constraints
				if (!string.IsNullOrEmpty(user.BadgeUid)) await SyncMieDevice(user.Id, "NFC_Badge", user.BadgeUid);
				if (!string.IsNullOrEmpty(user.CpsId)) await SyncMieDevice(user.Id, "CPS", user.CpsId);
				if (!string.IsNullOrEmpty(user.FidoId)) await SyncMieDevice(user.Id, "FIDO2", user.FidoId);

				// Process TempPin if provided for new user
				if (!string.IsNullOrEmpty(userDto.TempPin))
				{
					var userPin = new UserPin
					{
						OrganizationId = _organizationContext.OrganizationId.Value,
						Id = Guid.NewGuid(),
						UserId = user.Id,
						BadgeUid = activeBadgeUid!,
						PinHash = BCrypt.Net.BCrypt.HashPassword(userDto.TempPin),
						IsActive = true,
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow,
						CreatedBy = actorId
					};
					await _context.UserPins.AddAsync(userPin);
				}

				_auditService.Add(
					action: "Création de l'utilisateur local et définition des permissions",
					resourceType: "Utilisateur",
					resourceId: user.Id,
					newValues: $"SamAccountName: {user.SamAccountName}, Apps: {string.Join(",", userDto.AuthorizedApps ?? new List<string>())}"
				);
				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return CreatedAtAction(nameof(GetBySamAccountName), new { samAccountName = user.SamAccountName }, ToSafeUser(user, user.OrganizationId));
			}
			catch (Exception ex) when (Nexorsys.Identity.API.Services.DatabaseConcurrencyErrors.IsSerializationFailure(ex))
			{
				_logger.LogInformation(ex, "Local user creation was rejected after a concurrent database serialization conflict; identifier omitted.");
				return Conflict(new { code = "CONCURRENT_WRITE_RETRY" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating local user; identifier omitted.");
				return StatusCode(500, new { message = "User creation failed." });
			}
		}

		public class UserUpdateDto
		{
			public string? SamAccountName { get; set; }
			public string? Email { get; set; }
			public string? FirstName { get; set; }
			public string? LastName { get; set; }
			public string? Department { get; set; }
			public string? Title { get; set; }
			public string? Role { get; set; }
			public string? PasswordHash { get; set; }
			public string? BadgeUid { get; set; }
			public string? CpsId { get; set; }
			public string? FidoId { get; set; }
			public string? RppsNumber { get; set; }
			public string? MieType { get; set; }

			[Newtonsoft.Json.JsonProperty("isActive")]
			public bool? IsActive { get; set; }

			[Newtonsoft.Json.JsonProperty("mustChangePin")]
			public bool? MustChangePin { get; set; }

			public List<string>? AuthorizedApps { get; set; }
		}

		[HttpPut("{idOrSamAccountName}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> UpdateUser(string idOrSamAccountName, [FromBody] UserUpdateDto userUpdate)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			if (HasSensitiveAdministrativeChanges(userUpdate) && !HasAdminOnlyRole())
				return StatusCode(StatusCodes.Status403Forbidden, new { code = "ADMIN_ROLE_REQUIRED_FOR_SENSITIVE_USER_CHANGES" });
			var organizationId = _organizationContext.OrganizationId.Value;
			_logger.LogInformation("UpdateUser requested; targetHash={TargetHash}", IdentifierDigest(idOrSamAccountName));

			User? existing = null;
			if (Guid.TryParse(idOrSamAccountName, out var id))
			{
					existing = await _context.Users
						.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organizationId);
			}

			if (existing == null)
			{
				// Try by SamAccountName
				existing = await _context.Users
					.FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.SamAccountName == idOrSamAccountName);
				if (existing == null)
				{
					return NotFound();
				}
			}
			foreach (var credential in new[] { userUpdate.BadgeUid, userUpdate.CpsId, userUpdate.FidoId }
				.Where(value => !string.IsNullOrWhiteSpace(value)))
			{
				if (await CredentialOwnershipGuard.IsOwnedByAnotherIdentityAsync(_context, organizationId, existing.Id, credential))
					return Conflict(new { code = "CREDENTIAL_ALREADY_OWNED" });
			}

			id = existing.Id; // Ensure we have the Guid for auditing
			var oldIsActive = existing.IsActive;
			var oldRole = existing.Role;
			var oldValues = $"IsActive={oldIsActive}; Role={oldRole}";
			var statusChanged = false;
			var statusAfterSave = false;
			var samAccountNameChanged = false;

			if (!string.IsNullOrWhiteSpace(userUpdate.SamAccountName) &&
				!string.Equals(userUpdate.SamAccountName.Trim(), existing.SamAccountName, StringComparison.OrdinalIgnoreCase))
			{
				if (!existing.IsLocalProfile)
					return BadRequest(new { code = "DIRECTORY_IDENTIFIERS_ARE_READ_ONLY" });
				var newSam = userUpdate.SamAccountName.Trim();
				if (newSam.Contains(" "))
					return BadRequest("Le nom d'utilisateur ne doit pas contenir d'espaces.");
				if (await _context.Users.AnyAsync(u => u.OrganizationId == organizationId && u.Id != existing.Id &&
					u.SamAccountName == newSam))
					return Conflict(new { code = "IDENTIFIER_ALREADY_EXISTS" });
				existing.SamAccountName = newSam;
				samAccountNameChanged = true;
			}

			if (!string.IsNullOrEmpty(userUpdate.Email)) existing.Email = userUpdate.Email;
			if (!string.IsNullOrEmpty(userUpdate.FirstName)) existing.FirstName = userUpdate.FirstName;
			if (!string.IsNullOrEmpty(userUpdate.LastName)) existing.LastName = userUpdate.LastName;
			if (!string.IsNullOrEmpty(userUpdate.FirstName) || !string.IsNullOrEmpty(userUpdate.LastName))
			{
				existing.DisplayName = $"{existing.FirstName} {existing.LastName}".Trim();
			}
			if (!string.IsNullOrEmpty(userUpdate.Department)) existing.Department = userUpdate.Department;
			if (!string.IsNullOrEmpty(userUpdate.Title)) existing.Title = userUpdate.Title;
			if (!string.IsNullOrEmpty(userUpdate.Role))
			{
				if (!new[] { "VIEWER", "USER", "HR", "DIRECTOR", "ADMIN", "SUPERADMIN" }.Contains(userUpdate.Role, StringComparer.OrdinalIgnoreCase)) return BadRequest("Unsupported role.");
				if (!string.Equals(User.FindFirstValue(ClaimTypes.Role), "SUPERADMIN", StringComparison.OrdinalIgnoreCase)) return Forbid();
				existing.Role = userUpdate.Role.ToUpperInvariant();
			}
			if (userUpdate.BadgeUid != null)
			{
				existing.BadgeUid = userUpdate.BadgeUid;
				await SyncMieDevice(existing.Id, "NFC_Badge", userUpdate.BadgeUid);
			}
			if (userUpdate.CpsId != null)
			{
				existing.CpsId = userUpdate.CpsId.StartsWith("CSP-", StringComparison.OrdinalIgnoreCase) ? "CPS-" + userUpdate.CpsId.Substring(4) : userUpdate.CpsId;
				await SyncMieDevice(existing.Id, "CPS", existing.CpsId);
			}
			if (userUpdate.FidoId != null)
			{
				existing.FidoId = userUpdate.FidoId;
				await SyncMieDevice(existing.Id, "FIDO2", userUpdate.FidoId);
			}
			if (userUpdate.RppsNumber != null) existing.RppsNumber = userUpdate.RppsNumber;
			if (userUpdate.MieType != null) existing.BadgeType = userUpdate.MieType;
			if (userUpdate.IsActive.HasValue)
			{
				var oldStatus = existing.IsActive;
				_logger.LogInformation("UPDATING STATUS for user {Id}: {Old} -> {New}", id, oldStatus, userUpdate.IsActive.Value);
				var newState = userUpdate.IsActive.Value;
				existing.IsActive = newState;

				// Unblock user by resetting failed attempts if they are being reactivated
				if (newState)
				{
					existing.FailedPinAttempts = 0;
				}
				statusChanged = true;
				statusAfterSave = newState;
			}
			if (userUpdate.MustChangePin.HasValue)
			{
				existing.MustChangePin = userUpdate.MustChangePin.Value;
				_context.Entry(existing).Property(u => u.MustChangePin).IsModified = true;
			}

			if (!string.IsNullOrEmpty(userUpdate.PasswordHash))
			{
				if (userUpdate.PasswordHash.Contains(" "))
					return BadRequest("Le mot de passe ne doit pas contenir d'espaces.");
				existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(userUpdate.PasswordHash);
			}

			existing.UpdatedAt = DateTime.UtcNow;
			var currentPermissions = await _context.UserPermissions
				.Include(permission => permission.Application)
				.Where(permission => permission.OrganizationId == organizationId && permission.UserId == existing.Id)
				.ToListAsync();

			// Sync Permissions
			if (userUpdate.AuthorizedApps != null)
			{
				var allApps = await _context.Applications.Where(a => a.OrganizationId == organizationId).ToListAsync();

				existing.UserPermissions ??= new List<UserPermission>();

				// Map client IDs to database GUIDs
				var targetAppIds = new List<Guid>();
				foreach (var clientId in userUpdate.AuthorizedApps)
				{
					var app = allApps.FirstOrDefault(a => a.ClientId.Equals(clientId, StringComparison.OrdinalIgnoreCase));
					if (app == null)
					{
						// Auto-seed missing application from ManagedApps logic if possible
						// For now, we skip or could auto-create it if we want to be robust
						_logger.LogWarning("Application with ClientId {ClientId} not found in database. Skipping permission.", clientId);
						continue;
					}
					targetAppIds.Add(app.Id);
				}

				// 1. Remove permissions no longer in the list
				var permsToRemove = currentPermissions.Where(p => !targetAppIds.Contains(p.ApplicationId)).ToList();
				foreach (var perm in permsToRemove)
				{
					_context.UserPermissions.Remove(perm);
					currentPermissions.Remove(perm);
				}

				// 2. Add new permissions
				Guid? adminId = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(
					_context, User, organizationId);

				foreach (var appId in targetAppIds)
				{
					if (!currentPermissions.Any(p => p.ApplicationId == appId))
					{
						var newPerm = new UserPermission
						{
							OrganizationId = organizationId,
							Id = Guid.NewGuid(),
							UserId = existing.Id,
							ApplicationId = appId,
							PermissionLevel = "read",
							GrantedAt = DateTime.UtcNow,
							GrantedBy = adminId
						};
						existing.UserPermissions.Add(newPerm);
						_context.UserPermissions.Add(newPerm);
						currentPermissions.Add(newPerm);
						_logger.LogInformation("Adding new permission for App {AppId} to user {UserId}", appId, existing.Id);
					}
				}
			}

			// Sync active UserPin.BadgeUid to the CURRENTLY ACTIVE MIE identifier
			// This ensures kiosk auth works without PIN reset when switching/assigning devices.
				var activePin = await _context.UserPins
				.Where(p => p.OrganizationId == organizationId && p.UserId == id && p.IsActive)
				.OrderByDescending(p => p.CreatedAt)
				.FirstOrDefaultAsync();

			if (activePin != null)
			{
				string? targetIdentifier = existing.BadgeType switch
				{
					"NFC_Badge" => existing.BadgeUid,
					"CPS" => existing.CpsId,
					"FIDO2" => existing.FidoId,
					_ => existing.BadgeUid
				};

				var normalizedTarget = targetIdentifier?.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
				if (!string.IsNullOrEmpty(normalizedTarget) && activePin.BadgeUid != normalizedTarget)
				{
					_logger.LogInformation("Updating a user credential-to-PIN binding {PinId} (Type: {Type})", activePin.Id, existing.BadgeType);
					activePin.BadgeUid = normalizedTarget;
					activePin.UpdatedAt = DateTime.UtcNow;
				}
			}

			// Sync MIE Type if provided
			if (!string.IsNullOrEmpty(userUpdate.MieType))
			{
				// SyncMieDevice may have added the device during this request. EF queries
				// do not include Added entities, so consult the change tracker first to
				// avoid creating the same active device twice before SaveChangesAsync.
				var primaryDevice = _context.ChangeTracker.Entries<UserDevice>()
					.Where(entry => entry.State != EntityState.Deleted)
					.Select(entry => entry.Entity)
					.Where(d => d.OrganizationId == organizationId && d.UserId == existing.Id && d.Status == "active")
					.OrderByDescending(d => d.IsPrimary)
					.FirstOrDefault();

				primaryDevice ??= await _context.UserDevices
					.Where(d => d.OrganizationId == organizationId && d.UserId == existing.Id && d.Status == "active")
					.OrderByDescending(d => d.IsPrimary)
					.FirstOrDefaultAsync();

				if (primaryDevice != null)
				{
					if (primaryDevice.DeviceType != userUpdate.MieType)
					{
						primaryDevice.DeviceType = userUpdate.MieType;
						primaryDevice.DeviceName = userUpdate.MieType;
						primaryDevice.UpdatedAt = DateTime.UtcNow;
					}
				}
				else if (!string.IsNullOrEmpty(existing.BadgeUid))
				{
					// Create primary device if it doesn't exist but we have a UID
					var newDevice = new UserDevice
					{
						OrganizationId = organizationId,
						Id = Guid.NewGuid(),
						UserId = existing.Id,
						DeviceType = userUpdate.MieType,
						DeviceName = userUpdate.MieType,
						DeviceIdentifier = NormalizeDeviceIdentifier(existing.BadgeUid, userUpdate.MieType),
						Status = "active",
						IsPrimary = true,
						CreatedAt = DateTime.UtcNow,
						UpdatedAt = DateTime.UtcNow
					};
					_context.Set<UserDevice>().Add(newDevice);
				}
			}

			var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActor) ? parsedActor : (Guid?)null;
			var changedFields = new List<string>();
			if (samAccountNameChanged) changedFields.Add("SamAccountName");
			if (userUpdate.Email is not null) changedFields.Add("Email");
			if (userUpdate.FirstName is not null) changedFields.Add("FirstName");
			if (userUpdate.LastName is not null) changedFields.Add("LastName");
			if (userUpdate.Department is not null) changedFields.Add("Department");
			if (userUpdate.Title is not null) changedFields.Add("Title");
			if (userUpdate.Role is not null) changedFields.Add("Role");
			if (userUpdate.BadgeUid is not null || userUpdate.CpsId is not null || userUpdate.FidoId is not null || userUpdate.RppsNumber is not null)
				changedFields.Add("Credentials");
			if (userUpdate.MieType is not null) changedFields.Add("MieType");
			if (userUpdate.IsActive.HasValue) changedFields.Add("IsActive");
			if (userUpdate.MustChangePin.HasValue) changedFields.Add("MustChangePin");
			if (!string.IsNullOrEmpty(userUpdate.PasswordHash)) changedFields.Add("Password");
			var newValues = $"IsActive={existing.IsActive}; Role={existing.Role}; ChangedFields={string.Join(',', changedFields)}";
			_context.AuditLogs.Add(CreateUserAudit(existing.OrganizationId, actorId,
				"Mise à jour du profil utilisateur et des permissions", existing.Id, oldValues, newValues));
			if (oldIsActive != existing.IsActive)
				_context.AuditLogs.Add(CreateUserAudit(existing.OrganizationId, actorId,
					existing.IsActive ? "Réactivation Compte" : "Verrouillage Compte", existing.Id,
					$"IsActive={oldIsActive}", $"IsActive={existing.IsActive}"));

			// Commit the user, related ownership rows, and audit events together.
			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (IsActiveDeviceCredentialConflict(ex))
			{
				_logger.LogWarning(ex, "Rejected duplicate active device credential while updating user {UserId}.", existing.Id);
				return Conflict(new
				{
					code = "CREDENTIAL_ALREADY_OWNED",
					message = "Cette identité numérique est déjà attribuée à une autre identité."
				});
			}
			if (statusChanged)
			{
				try
				{
					_logger.LogInformation("SIGNALR: Broadcasting OnUserStatusChanged for user {UserId} (Active: {Status})", existing.Id, statusAfterSave);
					await _hubContext.Clients.Group(IdentityHub.AdminGroupName(existing.OrganizationId)).SendAsync("OnUserStatusChanged", new
					{
						UserId = id.ToString(),
						IsActive = statusAfterSave,
						SamAccountName = existing.SamAccountName,
						Timestamp = DateTime.UtcNow
					});
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "SIGNALR: Failed to broadcast user status change after persistence.");
				}
			}

			return Ok(new
			{
				id = existing.Id,
				isActive = existing.IsActive,
				displayName = existing.DisplayName,
				samAccountName = existing.SamAccountName,
				email = existing.Email,
				role = existing.Role
			});

		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteUser(Guid id)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			try
			{
				var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
				if (currentUserRole != "SUPERADMIN")
				{
					return Forbid("Seul un Super Administrateur peut supprimer des utilisateurs.");
				}

				if (!_organizationContext.OrganizationId.HasValue) return Forbid();
				var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == _organizationContext.OrganizationId.Value);
				if (user == null) return NotFound();

				if (user.SamAccountName == "administrator" || user.SamAccountName == "admin")
				{
					return BadRequest("Impossible de supprimer le compte Super Administrateur principal.");
				}

				var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActor) ? parsedActor : (Guid?)null;

				// Handle related entities to prevent foreign key constraint violations
				var auditLogs = await _context.AuditLogs.Where(a => a.UserId == id).ToListAsync();
				foreach(var al in auditLogs) { al.UserId = null; }

				var workflows = await _context.Workflows.Where(w => w.UserId == id).ToListAsync();
				_context.Workflows.RemoveRange(workflows);

				var sessions = await _context.Set<UserSession>().Where(s => s.UserId == id).ToListAsync();
				_context.Set<UserSession>().RemoveRange(sessions);

				var devices = await _context.Set<UserDevice>().Where(d => d.UserId == id).ToListAsync();
				_context.Set<UserDevice>().RemoveRange(devices);

				var workstations = await _context.Set<UserWorkstation>().Where(w => w.UserId == id).ToListAsync();
				_context.Set<UserWorkstation>().RemoveRange(workstations);

				_context.Users.Remove(user);
				_context.AuditLogs.Add(CreateUserAudit(user.OrganizationId, actorId,
					"Suppression du compte utilisateur", id, $"IsActive={user.IsActive}; Role={user.Role}", "UserRemoved=true"));
				await _context.SaveChangesAsync();

				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting user {Id}", id);
				return StatusCode(500, new { message = "User deletion failed." });
			}
		}

		[HttpGet("stats")]
		public async Task<IActionResult> GetStats()
		{
			try
			{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var totalUsers = await _context.Users.CountAsync(u => u.OrganizationId == _organizationContext.OrganizationId.Value);
				return Ok(new { TotalUsers = totalUsers });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting user statistics");
				return StatusCode(500, "Internal server error.");
			}
		}
		private async Task SyncMieDevice(Guid userId, string type, string identifier)
		{
			// Find existing device of this type
				if (!_organizationContext.OrganizationId.HasValue) return;
				var organizationId = _organizationContext.OrganizationId.Value;
				var device = await _context.UserDevices
				.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.UserId == userId && d.DeviceType == type);

			if (string.IsNullOrEmpty(identifier))
			{
				// If identifier is cleared, revoke the device entry if it exists
				if (device != null)
				{
					_logger.LogInformation("Revoking MIE device {Type} for user {UserId} because identifier was cleared.", type, userId);
					device.Status = "revoked";
					device.UpdatedAt = DateTime.UtcNow;
				}
				return;
			}

			// Normalization: Preserve dashes for long CPS strings
			string normalizedId = NormalizeDeviceIdentifier(identifier, type);

			if (device == null)
			{
				device = new UserDevice
				{
					OrganizationId = organizationId,
					Id = Guid.NewGuid(),
					UserId = userId,
					DeviceType = type,
					DeviceName = type switch
					{
						"NFC_Badge" => "Badge Personnel",
						"CPS" => "Carte CPS",
						"FIDO2" => "Clé Sécurité",
						_ => type
					},
					DeviceIdentifier = normalizedId,
					Status = "active",
					IsPrimary = true,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};
				await _context.UserDevices.AddAsync(device);
			}
			else
			{
				// Update existing device
				device.DeviceIdentifier = normalizedId;
				device.Status = "active";
				device.UpdatedAt = DateTime.UtcNow;
			}
		}

		private static string NormalizeDeviceIdentifier(string identifier, string type)
		{
			// CPS identifiers can contain meaningful separators; NFC UIDs and FIDO
			// identifiers are canonicalized so equivalent values cannot create
			// duplicate active rows under the unique database index.
			return type == "CPS" && identifier.Trim().Length > 20
				? identifier.Trim().ToUpperInvariant()
				: identifier.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
		}

		private static bool IsActiveDeviceCredentialConflict(DbUpdateException exception) =>
			exception.InnerException is PostgresException postgres &&
			postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
			postgres.ConstraintName == "ux_user_devices_org_type_identifier_active";

		private async Task<IActionResult?> RequireIdentityEntitlementAsync()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var decision = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
			return decision.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = decision.Code });
		}

		private static string IdentifierDigest(string? value)
		{
			if (string.IsNullOrWhiteSpace(value)) return "none";
			return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
				System.Text.Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant())))[..16];
		}

		private bool HasAdminOnlyRole() => new[] { "Admin", "ADMIN", "ADMIN_DSI", "SUPERADMIN" }
			.Contains(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

		private static bool HasSensitiveAdministrativeChanges(UserUpdateDto update) =>
			update.Role is not null || update.PasswordHash is not null || update.BadgeUid is not null ||
			update.CpsId is not null || update.FidoId is not null || update.RppsNumber is not null ||
			update.MieType is not null || update.IsActive.HasValue || update.MustChangePin.HasValue ||
			update.AuthorizedApps is not null;

		private AuditLog CreateUserAudit(Guid organizationId, Guid? actorId, string action, Guid userId,
			string? oldValues, string? newValues)
		{
			Nexorsys.Identity.API.Services.TenantActorOwnership.Ensure(_context, actorId, organizationId);
			return new AuditLog
			{
				Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId,
				Action = action, ResourceType = "Utilisateur", ResourceId = userId,
				OldValues = AuditRedactor.Redact(oldValues), NewValues = AuditRedactor.Redact(newValues),
				IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
				UserAgent = "api", CreatedAt = DateTime.UtcNow
			};
		}

		private static object ToSafeUser(User user, Guid organizationId) => new
		{
			user.Id, user.OrganizationId, user.SamAccountName, user.DisplayName, user.FirstName, user.LastName,
			user.Email, user.PhoneNumber, user.Department, user.Title, user.Role, user.IsActive,
			user.IsLocalProfile, user.MustChangePin, user.BadgeType, user.RppsNumber, user.CreatedAt, user.UpdatedAt,
			AuthorizedApps = (user.UserPermissions ?? new List<UserPermission>())
				.Where(p => p.OrganizationId == organizationId && p.Application?.OrganizationId == organizationId)
				.Select(p => p.Application!.ClientId).ToArray()
		};
	}
}
