using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IKioskService
	{
		Task<User?> IdentifyByBadgeAsync(string badgeUid);
		Task<User?> IdentifyByMieAsync(string identifier, string? type = null);
		Task<bool> ValidatePinAsync(Guid userId, string pin, string? badgeUid, Guid workstationId);
		Task<UserSession?> StartSessionAsync(Guid userId, string badgeUid, string nfcUid, Guid? workstationId = null);
		Task<bool> EndSessionAsync(string sessionToken);
		Task<bool> ResetPinAsync(Guid userId, string newPin, bool mustChangeNextTime = false);
		Task<bool> RevokeBadgeAsync(Guid userId);
		Task<bool> AssignBadgeAsync(Guid userId, string badgeUid);
		Task<bool> UnlockAccountAsync(Guid userId);
		Task<string> GenerateTemporaryPinAsync(Guid userId);
		Task<bool> RequestPinResetAsync(Guid userId, string? machineName = null);
		Task<object?> GetUserSecurityStatusAsync(Guid userId);
	}
}
