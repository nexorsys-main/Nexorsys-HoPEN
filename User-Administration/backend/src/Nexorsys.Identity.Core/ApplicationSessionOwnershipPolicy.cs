using System.Security.Cryptography;
using System.Text;

namespace Nexorsys.Identity.Core;

public static class ApplicationSessionOwnershipPolicy
{
	public static bool CanStop(ApplicationSession appSession, UserSession? ownerSession,
		Guid organizationId, Guid workstationId, string? presentedSessionToken,
		bool ownerUserIsActive, DateTime utcNow)
	{
		if (ownerSession is null || string.IsNullOrWhiteSpace(presentedSessionToken) || !ownerUserIsActive) return false;
		if (appSession.EndTime.HasValue || appSession.Id == Guid.Empty || appSession.UserSessionId != ownerSession.Id) return false;
		if (appSession.OrganizationId != organizationId || ownerSession.OrganizationId != organizationId) return false;
		if (appSession.WorkstationId != workstationId || ownerSession.WorkstationId != workstationId) return false;
		if (appSession.UserId != ownerSession.UserId || ownerSession.Status != "Active" || ownerSession.SessionEndedAt.HasValue) return false;
		if (ownerSession.SessionStartedAt < utcNow.AddHours(-12) || ownerSession.SessionStartedAt > utcNow.AddMinutes(1)) return false;
		if (!ownerSession.LastActivityAt.HasValue || ownerSession.LastActivityAt.Value <= utcNow.AddMinutes(-30) || ownerSession.LastActivityAt.Value > utcNow.AddMinutes(1)) return false;

		var expected = Encoding.UTF8.GetBytes(ownerSession.SessionToken ?? string.Empty);
		var presented = Encoding.UTF8.GetBytes(presentedSessionToken);
		return expected.Length >= 32 && expected.Length == presented.Length &&
			CryptographicOperations.FixedTimeEquals(expected, presented);
	}
}
