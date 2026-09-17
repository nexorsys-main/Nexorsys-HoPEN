using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using BCrypt.Net;

namespace Nexorsys.Identity.Infrastructure
{
	public class KioskService : IKioskService
	{
		private readonly AppDbContext _context;
		private readonly ILogger<KioskService> _logger;
		private readonly IOrganizationContext _organizationContext;
		private readonly IAuditService _audit;

		public KioskService(AppDbContext context, ILogger<KioskService> logger, IOrganizationContext organizationContext, IAuditService audit)
		{
			_context = context;
			_logger = logger;
			_organizationContext = organizationContext;
			_audit = audit;
		}

		public async Task<User?> IdentifyByBadgeAsync(string badgeUid)
		{
			return await IdentifyByMieAsync(badgeUid, "NFC_Badge");
		}

		public async Task<User?> IdentifyByMieAsync(string identifier, string? type = null)
		{
			if (string.IsNullOrWhiteSpace(identifier) || !_organizationContext.OrganizationId.HasValue) return null;
			if (identifier.Length > 512 || identifier.Any(char.IsControl)) return null;

			var normalized = NormalizeCredential(identifier);
			if (normalized.Length < 4) return null;
			var organizationId = _organizationContext.OrganizationId.Value;
			var now = DateTime.UtcNow;

			// Compare canonical exact identifiers across organizations so a duplicated
			// card can never select whichever tenant happened to be in request context.
			var devices = await _context.UserDevices.IgnoreQueryFilters()
				.Include(d => d.User)
				.Where(d => (string.IsNullOrEmpty(type) || d.DeviceType == type) &&
					((d.DeviceIdentifier != null && d.DeviceIdentifier.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized) ||
					 (d.DeviceSerial != null && d.DeviceSerial.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized)))
				.ToListAsync();

			var users = await _context.Users.IgnoreQueryFilters()
				.Where(u => ((string.IsNullOrEmpty(type) || type == "NFC_Badge") && u.BadgeUid != null &&
					u.BadgeUid.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized) ||
					((string.IsNullOrEmpty(type) || type == "CPS") && u.CpsId != null &&
					u.CpsId.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized))
				.ToListAsync();

			var matches = devices.Select(d => new
			{
				d.OrganizationId,
				d.UserId,
				User = d.User,
				IsValid = d.Status == "active" && (!d.ExpiresAt.HasValue || d.ExpiresAt > now) &&
					d.User != null && d.User.OrganizationId == d.OrganizationId && d.User.IsActive &&
					(!d.User.LockedUntil.HasValue || d.User.LockedUntil <= now)
			}).Concat(users.Select(u => new
			{
				OrganizationId = u.OrganizationId,
				UserId = u.Id,
				User = (User?)u,
				IsValid = u.IsActive && (!u.LockedUntil.HasValue || u.LockedUntil <= now)
			})).ToList();
			var owners = matches.GroupBy(x => new { x.OrganizationId, x.UserId }).ToList();

			// Zero, ambiguous, disabled/locked, foreign-tenant, or revoked credentials deny.
			if (owners.Count != 1 || owners[0].Key.OrganizationId != organizationId || owners[0].Any(x => !x.IsValid)) return null;
			_logger.LogInformation("Enrolled credential matched for organization {OrganizationId}", organizationId);
			return owners[0].Select(x => x.User).FirstOrDefault(u => u != null);
		}

		private static string NormalizeCredential(string? value)
		{
			if (string.IsNullOrWhiteSpace(value)) return string.Empty;
			var normalized = value.Trim();
			return normalized.Replace(":", "", StringComparison.Ordinal)
				.Replace("-", "", StringComparison.Ordinal)
				.Replace(" ", "", StringComparison.Ordinal)
				.ToUpperInvariant();
		}
		public async Task<bool> ValidatePinAsync(Guid userId, string pin, string? badgeUid, Guid workstationId)
		{
			if (!_organizationContext.OrganizationId.HasValue || userId == Guid.Empty || workstationId == Guid.Empty || string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(badgeUid)) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var now = DateTime.UtcNow;

			var workstationIsActive = await _context.Workstations.AnyAsync(w => w.Id == workstationId && w.OrganizationId == organizationId && w.IsActive);
			if (!workstationIsActive) return false;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId && u.IsActive);
			if (user == null) return false;

			// Check if locked
			if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
			{
				return false;
			}

			bool isValid = false;

			// Fetch the active PIN for this user
			var userPin = await _context.UserPins
				.Where(p => p.OrganizationId == organizationId && p.UserId == userId && p.IsActive)
				.OrderByDescending(p => p.UpdatedAt)
				.FirstOrDefaultAsync();

			if (userPin == null)
			{
				_logger.LogWarning("No active PIN found in database for user {UserId}", userId);
				return false;
			}

			var normalizedBadge = NormalizeCredential(badgeUid);
			if (normalizedBadge.Length < 4 || normalizedBadge != NormalizeCredential(userPin.BadgeUid)) return false;
			// Search exact normalized values across tenants to detect duplicate ownership,
			// but never materialize every tenant's credential inventory into this process.
			var matchingRecords = await _context.UserDevices.IgnoreQueryFilters().Where(d =>
				(d.DeviceIdentifier != null && d.DeviceIdentifier.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalizedBadge) ||
				(d.DeviceSerial != null && d.DeviceSerial.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalizedBadge))
				.ToListAsync();
			var ownPrimaryCredential = NormalizeCredential(user.BadgeUid) == normalizedBadge || NormalizeCredential(user.CpsId) == normalizedBadge;
			if (matchingRecords.Count > 0 && matchingRecords.Any(d => d.Status != "active" ||
				(d.ExpiresAt.HasValue && d.ExpiresAt <= now) || d.OrganizationId != organizationId || d.UserId != userId)) return false;
			if (matchingRecords.Count == 0 && !ownPrimaryCredential) return false;

			if (!isValid)
			{
				try
				{
					isValid = BCrypt.Net.BCrypt.Verify(pin, userPin.PinHash);
					_logger.LogInformation("BCrypt verification result for user {UserId}: {Result}", userId, isValid);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "BCrypt verification threw an exception for user {UserId}", userId);
					isValid = false;
				}
			}

			return await RecordPinAttemptAsync(user, organizationId, isValid, now);
		}

		private async Task<bool> RecordPinAttemptAsync(User user, Guid organizationId, bool credentialValid, DateTime now)
		{
			if (!_context.Database.IsRelational())
			{
				if (credentialValid)
				{
					user.FailedPinAttempts = 0;
					user.LockedUntil = null;
					user.LastLoginAt = now;
				}
				else
				{
					user.FailedPinAttempts++;
					if (user.FailedPinAttempts >= 3) user.LockedUntil = now.AddMinutes(5);
				}
				user.UpdatedAt = now;
				_audit.Add(credentialValid ? "Kiosk PIN authentication accepted" :
					(user.LockedUntil > now ? "Kiosk account locked after PIN failures" : "Kiosk PIN authentication denied"),
					"Authentication", user.Id,
					newValues: $"FailedPinAttempts={user.FailedPinAttempts}; Locked={user.LockedUntil > now}", userId: user.Id);
				await _context.SaveChangesAsync();
				return credentialValid;
			}

			// SQL-side arithmetic prevents parallel failed attempts from overwriting each
			// other's counters. The row update and its audit share one transaction.
			await using var transaction = await _context.Database.BeginTransactionAsync();
			var isValid = credentialValid;
			if (credentialValid)
			{
				var reset = await _context.Users.IgnoreQueryFilters().Where(u => u.Id == user.Id &&
					u.OrganizationId == organizationId && u.IsActive && (!u.LockedUntil.HasValue || u.LockedUntil <= now))
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(u => u.FailedPinAttempts, 0)
						.SetProperty(u => u.LockedUntil, (DateTime?)null)
						.SetProperty(u => u.LastLoginAt, now)
						.SetProperty(u => u.UpdatedAt, now));
				isValid = reset == 1;
			}
			else
			{
				await _context.Users.IgnoreQueryFilters().Where(u => u.Id == user.Id && u.OrganizationId == organizationId &&
					u.IsActive && (!u.LockedUntil.HasValue || u.LockedUntil <= now))
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(u => u.FailedPinAttempts, u => u.FailedPinAttempts + 1)
						.SetProperty(u => u.LockedUntil, u => u.FailedPinAttempts + 1 >= 3 ? now.AddMinutes(5) : u.LockedUntil)
						.SetProperty(u => u.UpdatedAt, now));
			}

			var state = await _context.Users.IgnoreQueryFilters().AsNoTracking()
				.Where(u => u.Id == user.Id && u.OrganizationId == organizationId)
				.Select(u => new { u.FailedPinAttempts, u.LockedUntil })
				.SingleOrDefaultAsync();
			var failedAttempts = state?.FailedPinAttempts ?? user.FailedPinAttempts;
			var lockedUntil = state is null ? user.LockedUntil : state.LockedUntil;
			_context.Entry(user).State = EntityState.Detached;
			user.FailedPinAttempts = failedAttempts;
			user.LockedUntil = lockedUntil;
			if (isValid) user.LastLoginAt = now;
			user.UpdatedAt = now;

			_audit.Add(isValid ? "Kiosk PIN authentication accepted" :
				(lockedUntil > now ? "Kiosk account locked after PIN failures" : "Kiosk PIN authentication denied"),
				"Authentication", user.Id,
				newValues: $"FailedPinAttempts={failedAttempts}; Locked={lockedUntil > now}", userId: user.Id);
			await _context.SaveChangesAsync();
			await transaction.CommitAsync();
			return isValid;
		}

		public async Task<UserSession?> StartSessionAsync(Guid userId, string badgeUid, string nfcUid, Guid? workstationId = null)
		{
			if (!_organizationContext.OrganizationId.HasValue) return null;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == _organizationContext.OrganizationId.Value && u.IsActive &&
				(!u.LockedUntil.HasValue || u.LockedUntil <= DateTime.UtcNow));
			if (user is null || !workstationId.HasValue ||
				!await _context.Workstations.AnyAsync(w => w.Id == workstationId.Value && w.OrganizationId == _organizationContext.OrganizationId.Value && w.IsActive))
				return null;

			var session = new UserSession
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organizationContext.OrganizationId.Value,
				UserId = userId,
				WorkstationId = workstationId,
				SessionToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
				SessionStartedAt = DateTime.UtcNow,
				LastActivityAt = DateTime.UtcNow,
				Status = "Active",
				Department = user.Department,
				AuthenticationMethod = "NfcBadgeAndPin"
			};

			_context.UserSessions.Add(session);
			_audit.Add("Kiosk session created", "UserSession", session.Id,
				newValues: $"WorkstationId={session.WorkstationId}; Method={session.AuthenticationMethod}", userId: userId);
			await _context.SaveChangesAsync();

			return session;
		}

		public async Task<bool> EndSessionAsync(string sessionToken)
		{
			if (!_organizationContext.OrganizationId.HasValue || string.IsNullOrWhiteSpace(sessionToken)) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var session = await _context.UserSessions.FirstOrDefaultAsync(s =>
				s.OrganizationId == organizationId && s.SessionToken == sessionToken && s.Status == "Active" && s.SessionEndedAt == null);

			if (session == null) return false;

			session.Status = "Closed";
			session.SessionEndedAt = DateTime.UtcNow;
			_audit.Add("Kiosk session ended", "UserSession", session.Id,
				newValues: "Status=Closed", userId: session.UserId);
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> ResetPinAsync(Guid userId, string newPin, bool mustChangeNextTime = false)
		{
			if (!_organizationContext.OrganizationId.HasValue || userId == Guid.Empty || string.IsNullOrWhiteSpace(newPin)) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null) return false;

			// Helper to normalize badge UIDs
			Func<string, string> normalize = (s) =>
			{
				if (string.IsNullOrEmpty(s)) return s;
				if (s.StartsWith("CSP-", StringComparison.OrdinalIgnoreCase)) s = "CPS-" + s.Substring(4);
				return (s.Length > 20) ? s.Trim().ToUpper() : s.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToUpper();
			};

			// Determine the raw identifier to use for the PIN
			string? badgeUidForPin = user.BadgeUid;
			if (string.IsNullOrEmpty(badgeUidForPin))
			{
				badgeUidForPin = user.BadgeType switch
				{
					"CPS" => user.CpsId,
					"FIDO2" => user.FidoId,
					_ => user.CpsId ?? user.FidoId ?? Guid.NewGuid().ToString()
				};
			}
			if (string.IsNullOrEmpty(badgeUidForPin)) badgeUidForPin = Guid.NewGuid().ToString();

			// Normalize the target badge UID for PIN record
			string normalizedBadgeForPin = normalize(badgeUidForPin);
			if (string.IsNullOrWhiteSpace(normalizedBadgeForPin)) return false;

			// Badge/PIN ownership is never transferred implicitly, including when a
			// SystemAdmin bypasses EF's tenant query filters.
			var foreignUserOwnsBadge = await _context.Users.IgnoreQueryFilters().AnyAsync(u =>
				u.OrganizationId != organizationId && u.Id != userId && u.BadgeUid != null &&
			(u.BadgeUid.Length > 20 ? u.BadgeUid.Trim().ToUpper() : u.BadgeUid.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == normalizedBadgeForPin);
			var foreignPinOwnsBadge = await _context.UserPins.IgnoreQueryFilters().AnyAsync(p =>
				p.OrganizationId != organizationId && p.UserId != userId && p.BadgeUid != null &&
				(p.BadgeUid.Length > 20 ? p.BadgeUid.Trim().ToUpper() : p.BadgeUid.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == normalizedBadgeForPin);
			var foreignDeviceOwnsBadge = await _context.UserDevices.IgnoreQueryFilters().AnyAsync(d => d.OrganizationId != organizationId &&
				d.Status == "active" && ((d.DeviceIdentifier != null && (d.DeviceIdentifier.Length > 20 ? d.DeviceIdentifier.Trim().ToUpper() : d.DeviceIdentifier.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == normalizedBadgeForPin) ||
				(d.DeviceSerial != null && (d.DeviceSerial.Length > 20 ? d.DeviceSerial.Trim().ToUpper() : d.DeviceSerial.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == normalizedBadgeForPin)));
			var sameTenantPinBadges = await _context.UserPins.IgnoreQueryFilters()
				.Where(p => p.OrganizationId == organizationId && p.UserId != userId)
				.Select(p => p.BadgeUid).ToListAsync();
			var sameTenantPinConflict = sameTenantPinBadges.Any(badge => normalize(badge) == normalizedBadgeForPin);
			if (foreignUserOwnsBadge || foreignPinOwnsBadge || foreignDeviceOwnsBadge || sameTenantPinConflict) return false;

			// Deactivate ALL existing pins for this user and invalidate their BadgeUid to free it up for the new one
			var allUserPins = await _context.UserPins
				.Where(p => p.OrganizationId == organizationId && p.UserId == userId)
				.ToListAsync();

			foreach (var pin in allUserPins)
			{
				pin.IsActive = false;
				pin.UpdatedAt = DateTime.UtcNow;
				// Append Guid to BadgeUid to satisfy UNIQUE constraint for historical entries
				string normPinBadge = normalize(pin.BadgeUid);
				if (!normPinBadge.Contains("_OLD_"))
				{
					pin.BadgeUid = $"{normPinBadge}_OLD_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
				}
			}

			var newPinEntry = new UserPin
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				UserId = userId,
				BadgeUid = normalizedBadgeForPin, // Save the normalized form to avoid spacing/colon index collisions!
				PinHash = BCrypt.Net.BCrypt.HashPassword(newPin.Trim()),
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
			await _context.UserPins.AddAsync(newPinEntry);

			// Also unlock the account when resetting PIN
			user.FailedPinAttempts = 0;
			user.LockedUntil = null;
			user.MustChangePin = mustChangeNextTime;
			user.UpdatedAt = DateTime.UtcNow;

			_audit.Add("Kiosk PIN reset", "User", userId,
				newValues: $"MustChangePin={mustChangeNextTime}; PIN value omitted.");
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> RevokeBadgeAsync(Guid userId)
		{
			if (!_organizationContext.OrganizationId.HasValue) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null) return false;

			user.BadgeUid = null;
			user.UpdatedAt = DateTime.UtcNow;

			var activePins = await _context.UserPins
				.Where(p => p.OrganizationId == organizationId && p.UserId == userId && p.IsActive)
				.ToListAsync();

			foreach (var pin in activePins) pin.IsActive = false;

			// Also update MIE device registry: revoke both legacy NFC and newer CPS devices
			var devices = await _context.UserDevices
				.Where(d => d.OrganizationId == organizationId && d.UserId == userId && (d.DeviceType == "NFC_Badge" || d.DeviceType == "CPS") && d.Status == "active")
				.ToListAsync();

			foreach (var d in devices)
			{
				d.Status = "revoked";
				d.UpdatedAt = DateTime.UtcNow;
			}

			_audit.Add("Kiosk badge revoked", "User", userId,
				newValues: $"ActivePinsRevoked={activePins.Count}; ActiveDevicesRevoked={devices.Count}; credential values omitted.");
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> AssignBadgeAsync(Guid userId, string badgeUid)
		{
			if (!_organizationContext.OrganizationId.HasValue || string.IsNullOrWhiteSpace(badgeUid)) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null) return false;

			// Normalize badge UID: remove separators and store as clean uppercase hex
			// Normalization: Consistent with IdentifyByMieAsync
			badgeUid = (badgeUid.Length > 20)
				? badgeUid.Trim().ToUpper()
				: badgeUid.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToUpper();
			if (badgeUid.StartsWith("CSP", StringComparison.Ordinal)) badgeUid = "CPS" + badgeUid[3..];

			var foreignOwner = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.OrganizationId != organizationId &&
				u.Id != userId && u.BadgeUid != null &&
				(u.BadgeUid.Length > 20 ? u.BadgeUid.Trim().ToUpper() : u.BadgeUid.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == badgeUid);
			var foreignDeviceOwner = await _context.UserDevices.IgnoreQueryFilters().AnyAsync(d => d.OrganizationId != organizationId &&
				d.Status == "active" && ((d.DeviceIdentifier != null && (d.DeviceIdentifier.Length > 20 ? d.DeviceIdentifier.Trim().ToUpper() : d.DeviceIdentifier.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == badgeUid) ||
				(d.DeviceSerial != null && (d.DeviceSerial.Length > 20 ? d.DeviceSerial.Trim().ToUpper() : d.DeviceSerial.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == badgeUid)));
			var foreignPinOwner = await _context.UserPins.IgnoreQueryFilters().AnyAsync(p => p.OrganizationId != organizationId && p.BadgeUid != null &&
				(p.BadgeUid.Length > 20 ? p.BadgeUid.Trim().ToUpper() : p.BadgeUid.Trim().ToUpper().Replace("CSP-", "CPS-").Replace(":", "").Replace("-", "").Replace(" ", "")) == badgeUid);
			if (foreignOwner || foreignDeviceOwner || foreignPinOwner) return false;

			// Remove this badge UID from any other user who currently holds it
			var previousOwners = await _context.Users
				.Where(u => u.OrganizationId == organizationId && u.BadgeUid == badgeUid && u.Id != userId)
				.ToListAsync();

			foreach (var prev in previousOwners)
			{
				prev.BadgeUid = null;
				prev.UpdatedAt = DateTime.UtcNow;

				// Also deactivate their pins so they can't authenticate with a revoked badge
				var prevPins = await _context.UserPins
					.Where(p => p.OrganizationId == organizationId && p.UserId == prev.Id && p.IsActive)
					.ToListAsync();
				foreach (var pin in prevPins) pin.IsActive = false;
				var prevDevices = await _context.UserDevices
					.Where(d => d.OrganizationId == organizationId && d.UserId == prev.Id && d.Status == "active" &&
						(d.DeviceType == "NFC_Badge" || d.DeviceType == "CPS"))
					.ToListAsync();
				foreach (var device in prevDevices) { device.Status = "revoked"; device.UpdatedAt = DateTime.UtcNow; }
			}

			user.BadgeUid = badgeUid;
			user.UpdatedAt = DateTime.UtcNow;

			// Also update MIE device registry
			var existingDevice = await _context.UserDevices
				.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.UserId == userId && d.DeviceType == "NFC_Badge" && d.DeviceIdentifier == badgeUid);

			if (existingDevice == null)
			{
				await _context.UserDevices.AddAsync(new UserDevice
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					UserId = userId,
					DeviceType = "NFC_Badge",
					DeviceName = "Badge NFC (Kiosque)",
					DeviceIdentifier = badgeUid,
					Status = "active",
					AssuranceLevel = "Intermediate",
					IsPrimary = true,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
			}
			else
			{
				existingDevice.Status = "active";
				existingDevice.UpdatedAt = DateTime.UtcNow;
			}

			_audit.Add("Kiosk badge assigned", "User", userId,
				newValues: "Badge credential assigned; identifier omitted.");
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> UnlockAccountAsync(Guid userId)
		{
			if (!_organizationContext.OrganizationId.HasValue) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (user == null) return false;

			user.FailedPinAttempts = 0;
			user.LockedUntil = null;
			user.UpdatedAt = DateTime.UtcNow;

			_audit.Add("Kiosk account unlocked", "User", userId,
				newValues: "FailedPinAttempts=0; LockedUntil=null.");
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<string> GenerateTemporaryPinAsync(Guid userId)
		{
			// This method is called only by the development-only controller path. The
			// production endpoint remains disabled until out-of-band delivery exists.
			var pin = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
			if (!await ResetPinAsync(userId, pin))
				throw new InvalidOperationException("The temporary PIN could not be assigned to this user.");
			return pin;
		}

		public async Task<bool> RequestPinResetAsync(Guid userId, string? machineName = null)
		{
			if (!_organizationContext.OrganizationId.HasValue) return false;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId && u.IsActive);
			if (user == null) return false;

			// Check if a pending request already exists to avoid duplicates
			var existing = await _context.Workflows
				.FirstOrDefaultAsync(w => w.OrganizationId == organizationId && w.UserId == userId && w.Type == "PIN_RESET_REQUEST" && w.Status == "pending");
			Guid workflowId;

			if (existing != null)
			{
				workflowId = existing.Id;
				existing.UpdatedAt = DateTime.UtcNow;
				existing.Comments = $"Rappel de demande depuis {machineName ?? "Kiosque"}";
			}
			else
			{
				var workflow = new Workflow
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					UserId = userId,
					Type = "PIN_RESET_REQUEST",
					Status = "pending",
					Comments = $"Demande de nouveau code PIN effectuée depuis le kiosque {machineName ?? ""}",
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};
				workflowId = workflow.Id;
				await _context.Workflows.AddAsync(workflow);
			}

			_audit.Add("Kiosk PIN reset requested", "Workflow", workflowId,
				newValues: $"Type=PIN_RESET_REQUEST; Status=pending; workstation={machineName ?? "Kiosque"}");
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<object?> GetUserSecurityStatusAsync(Guid userId)
		{
			if (!_organizationContext.OrganizationId.HasValue) return null;
			var organizationId = _organizationContext.OrganizationId.Value;
			var user = await _context.Users
				.Include(u => u.UserPins!.Where(p => p.OrganizationId == organizationId))
				.FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId);

			if (user == null) return null;

			var activePin = user.UserPins?.FirstOrDefault(p => p.IsActive);
			bool isLocked = user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow;

			// Fallback: If legacy BadgeUid is null, look for a primary active MIE device
			var badgeUid = user.BadgeUid;
			string? deviceType = "NFC_Badge";

			if (string.IsNullOrEmpty(badgeUid))
			{
				var primaryDevice = await _context.UserDevices
					.Where(d => d.OrganizationId == organizationId && d.UserId == userId && d.Status == "active")
					.OrderByDescending(d => d.IsPrimary)
					.FirstOrDefaultAsync();

				if (primaryDevice != null)
				{
					badgeUid = primaryDevice.DeviceIdentifier;
					deviceType = primaryDevice.DeviceType;
				}
			}

			return new
			{
				UserId = user.Id,
				DisplayName = user.DisplayName,
				HasBadge = !string.IsNullOrWhiteSpace(badgeUid),
				DeviceType = deviceType,
				HasActivePin = activePin != null,
				LastPinChange = activePin?.CreatedAt,
				IsAccountLocked = isLocked,
				LockedUntil = user.LockedUntil,
				FailedAttempts = user.FailedPinAttempts
			};
		}
	}
}
