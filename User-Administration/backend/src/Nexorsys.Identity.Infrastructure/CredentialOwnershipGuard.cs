using Microsoft.EntityFrameworkCore;

namespace Nexorsys.Identity.Infrastructure;

/// <summary>
/// Prevents credential assignment paths from introducing cross-tenant ambiguous
/// ownership. Identification fails closed on ambiguous credentials, so writes must
/// enforce the same canonical comparison before persisting a new binding.
/// </summary>
public static class CredentialOwnershipGuard
{
	public static async Task<bool> IsOwnedByAnotherIdentityAsync(
		AppDbContext context,
		Guid organizationId,
		Guid userId,
		string? credential,
		CancellationToken cancellationToken = default)
	{
		var normalized = Normalize(credential);
		if (normalized.Length == 0) return false;

		var userConflict = await context.Users.IgnoreQueryFilters().AnyAsync(user =>
			(user.OrganizationId != organizationId || user.Id != userId) &&
			((user.BadgeUid != null && user.BadgeUid.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized) ||
			 (user.CpsId != null && user.CpsId.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized) ||
			 (user.FidoId != null && user.FidoId.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized)), cancellationToken);
		if (userConflict) return true;

		var deviceConflict = await context.UserDevices.IgnoreQueryFilters().AnyAsync(device =>
			(device.OrganizationId != organizationId || device.UserId != userId) &&
			((device.DeviceIdentifier != null && device.DeviceIdentifier.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized) ||
			 (device.DeviceSerial != null && device.DeviceSerial.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized)), cancellationToken);
		if (deviceConflict) return true;

		return await context.UserPins.IgnoreQueryFilters().AnyAsync(pin =>
			(pin.OrganizationId != organizationId || pin.UserId != userId) && pin.BadgeUid != null &&
			pin.BadgeUid.Trim().ToUpper().Replace(":", "").Replace("-", "").Replace(" ", "") == normalized, cancellationToken);
	}

	public static string Normalize(string? credential) => string.IsNullOrWhiteSpace(credential)
		? string.Empty
		: credential.Trim().ToUpperInvariant().Replace(":", "", StringComparison.Ordinal)
			.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

}
