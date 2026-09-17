using System.Security.Cryptography;
using System.Text.Json;

namespace Nexorsys.Identity.Core;

public sealed record LicenseRevocationSnapshot(long Sequence, DateTimeOffset IssuedAt,
	DateTimeOffset ExpiresAt, string[] RevokedLicenseIds);

public sealed record RevocationSnapshotVerification(bool IsValid, string Code, LicenseRevocationSnapshot? Snapshot = null);

/// <summary>Verifies vendor-signed, short-lived license revocation snapshots. No signing key is present here.</summary>
public static class SignedLicenseRevocationSnapshotVerifier
{
	private static readonly TimeSpan MaximumValidity = TimeSpan.FromDays(7);

	/// <summary>
	/// Reads the sequence from a signature-authentic snapshot without treating it as currently usable.
	/// This lets durable caches preserve their monotonic floor after the snapshot expires.
	/// </summary>
	public static bool TryReadSignedSequence(string? token, string? publicKeyPem, out long sequence)
	{
		sequence = 0;
		if (string.IsNullOrWhiteSpace(publicKeyPem) || string.IsNullOrWhiteSpace(token) || token.Length > 65_536)
			return false;
		var parts = token.Split('.');
		if (parts.Length != 2 || !TryDecode(parts[0], out var payload) || !TryDecode(parts[1], out var signature))
			return false;
		try
		{
			using var key = ECDsa.Create();
			key.ImportFromPem(publicKeyPem);
			if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
				return false;
			var snapshot = JsonSerializer.Deserialize<LicenseRevocationSnapshot>(payload);
			if (snapshot is null || snapshot.Sequence < 1) return false;
			sequence = snapshot.Sequence;
			return true;
		}
		catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
		{
			return false;
		}
	}

	public static RevocationSnapshotVerification Verify(string? token, string? publicKeyPem, DateTimeOffset now)
	{
		if (string.IsNullOrWhiteSpace(publicKeyPem) || string.IsNullOrWhiteSpace(token) || token.Length > 65_536)
			return new(false, "REVOCATION_SNAPSHOT_NOT_CONFIGURED");
		var parts = token.Split('.');
		if (parts.Length != 2 || !TryDecode(parts[0], out var payload) || !TryDecode(parts[1], out var signature))
			return new(false, "REVOCATION_SNAPSHOT_INVALID_FORMAT");
		try
		{
			using var key = ECDsa.Create();
			key.ImportFromPem(publicKeyPem);
			if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
				return new(false, "REVOCATION_SNAPSHOT_SIGNATURE_INVALID");
		}
		catch (CryptographicException) { return new(false, "REVOCATION_SNAPSHOT_KEY_INVALID"); }
		LicenseRevocationSnapshot? snapshot;
		try { snapshot = JsonSerializer.Deserialize<LicenseRevocationSnapshot>(payload); }
		catch (JsonException) { return new(false, "REVOCATION_SNAPSHOT_INVALID_PAYLOAD"); }
		if (snapshot is null || snapshot.Sequence < 1 || snapshot.RevokedLicenseIds is null || snapshot.RevokedLicenseIds.Length > 1_000_000 ||
			snapshot.IssuedAt > now.AddMinutes(5) || snapshot.ExpiresAt <= now || snapshot.ExpiresAt <= snapshot.IssuedAt ||
			snapshot.ExpiresAt - snapshot.IssuedAt > MaximumValidity || snapshot.RevokedLicenseIds.Any(string.IsNullOrWhiteSpace) ||
			snapshot.RevokedLicenseIds.Any(id => id.Length > 256) || snapshot.RevokedLicenseIds.Distinct(StringComparer.Ordinal).Count() != snapshot.RevokedLicenseIds.Length)
			return new(false, "REVOCATION_SNAPSHOT_INVALID_OR_EXPIRED");
		return new(true, "REVOCATION_SNAPSHOT_VALID", snapshot);
	}

	private static bool TryDecode(string value, out byte[] data)
	{
		data = Array.Empty<byte>();
		try
		{
			var normalized = value.Replace('-', '+').Replace('_', '/');
			normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
			data = Convert.FromBase64String(normalized);
			return true;
		}
		catch (FormatException) { return false; }
	}
}
