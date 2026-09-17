using System.Security.Cryptography;
using System.Text.Json;

namespace Nexorsys.Identity.Core;

public sealed record LicenseClaims(
	string LicenseId,
	Guid OrganizationId,
	string Product,
	string MinimumVersion,
	string MaximumVersion,
	DateTimeOffset NotBefore,
	DateTimeOffset ExpiresAt,
	bool IsTrial,
	bool IsRevoked,
	int LicensedUsers,
	int LicensedWorkstations,
	string[] Modules);

public sealed record LicenseVerification(bool IsValid, string Code, LicenseClaims? Claims = null);

/// <summary>Verifies vendor-signed license tokens. This assembly contains only public-key verification code.</summary>
public static class SignedLicenseVerifier
{
	public static LicenseVerification Verify(string? token, string? trustedPublicKeyPem,
		Guid expectedOrganizationId, string expectedProduct, string productVersion,
		IReadOnlySet<string>? revokedLicenseIds, DateTimeOffset now)
	{
		if (string.IsNullOrWhiteSpace(trustedPublicKeyPem)) return new(false, "LICENSE_VERIFIER_NOT_CONFIGURED");
		if (string.IsNullOrWhiteSpace(token) || token.Length > 32_768) return new(false, "LICENSE_INVALID_FORMAT");
		var parts = token.Split('.');
		if (parts.Length != 2 || !TryBase64Url(parts[0], out var payload) || !TryBase64Url(parts[1], out var signature) || payload.Length > 16_384)
			return new(false, "LICENSE_INVALID_FORMAT");

		try
		{
			using var key = ECDsa.Create();
			key.ImportFromPem(trustedPublicKeyPem);
			if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
				return new(false, "LICENSE_SIGNATURE_INVALID");
		}
		catch (CryptographicException) { return new(false, "LICENSE_VERIFIER_INVALID"); }

		LicenseClaims? claims;
		try { claims = JsonSerializer.Deserialize<LicenseClaims>(payload); }
		catch (JsonException) { return new(false, "LICENSE_INVALID_PAYLOAD"); }
		if (claims is null || string.IsNullOrWhiteSpace(claims.LicenseId) || claims.OrganizationId == Guid.Empty ||
			claims.LicensedUsers < 1 || claims.LicensedWorkstations < 1 || claims.Modules is null)
			return new(false, "LICENSE_INVALID_PAYLOAD");
		if (claims.IsRevoked || revokedLicenseIds?.Contains(claims.LicenseId) == true) return new(false, "LICENSE_REVOKED");
		if (claims.OrganizationId != expectedOrganizationId) return new(false, "LICENSE_WRONG_ORGANIZATION");
		if (!string.Equals(claims.Product, expectedProduct, StringComparison.Ordinal)) return new(false, "LICENSE_WRONG_PRODUCT");
		if (!Version.TryParse(claims.MinimumVersion, out var minimum) || !Version.TryParse(claims.MaximumVersion, out var maximum) ||
			!Version.TryParse(productVersion, out var current) || minimum > maximum || current < minimum || current > maximum)
			return new(false, "LICENSE_VERSION_NOT_COVERED");
		if (claims.NotBefore > claims.ExpiresAt) return new(false, "LICENSE_INVALID_DATE_RANGE");
		if (claims.NotBefore > now) return new(false, "LICENSE_NOT_YET_VALID");
		if (claims.ExpiresAt <= now) return new(false, claims.IsTrial ? "TRIAL_EXPIRED" : "LICENSE_EXPIRED");
		if (claims.IsTrial && claims.ExpiresAt - claims.NotBefore > TimeSpan.FromDays(31)) return new(false, "LICENSE_INVALID_TRIAL_TERM");
		return new(true, "LICENSE_VALID", claims);
	}

	private static bool TryBase64Url(string value, out byte[] data)
	{
		data = Array.Empty<byte>();
		try
		{
			var base64 = value.Replace('-', '+').Replace('_', '/');
			base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
			data = Convert.FromBase64String(base64);
			return true;
		}
		catch (FormatException) { return false; }
	}
}
