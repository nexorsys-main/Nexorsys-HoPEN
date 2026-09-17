using System.Security.Cryptography;

namespace Nexorsys.Identity.Core;

/// <summary>Windows drive-path normalization that behaves consistently on Windows and Linux API hosts.</summary>
public static class WindowsApplicationIdentityPolicy
{
	public static string? NormalizeAbsolutePath(string? value, bool directory)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length > 1024) return null;
		var path = value.Trim().Replace('/', '\\');
		if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\') return null;
		var segments = path[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
		var normalized = new List<string>(segments.Length);
		foreach (var segment in segments)
		{
			if (segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') ||
				segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*', '\0']) >= 0) return null;
			normalized.Add(segment);
		}
		if (!directory && normalized.Count == 0) return null;
		var result = char.ToUpperInvariant(path[0]) + @":\" + string.Join('\\', normalized);
		return directory || normalized.Count == 0 ? result.TrimEnd('\\') + @"\" : result;
	}

	public static bool IsWithinInstallRoot(string? installRoot, string? executablePath)
	{
		var root = NormalizeAbsolutePath(installRoot, directory: true);
		var executable = NormalizeAbsolutePath(executablePath, directory: false);
		return root is not null && executable is not null && executable.StartsWith(root, StringComparison.OrdinalIgnoreCase);
	}

	public static bool PublisherMatches(string? expectedThumbprint, string? observedThumbprint)
	{
		static string Normalize(string? value) => (value ?? string.Empty).Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
		var expected = Normalize(expectedThumbprint);
		var actual = Normalize(observedThumbprint);
		return expected.Length == 40 && actual.Length == 40 && expected.All(Uri.IsHexDigit) && actual.All(Uri.IsHexDigit) && CryptographicOperations.FixedTimeEquals(
			Convert.FromHexString(expected), Convert.FromHexString(actual));
	}

	public static bool Sha256Matches(string? expectedHash, string? observedHash)
	{
		static string Normalize(string? value) => (value ?? string.Empty).Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
		var expected = Normalize(expectedHash);
		var actual = Normalize(observedHash);
		return expected.Length == 64 && actual.Length == 64 && expected.All(Uri.IsHexDigit) && actual.All(Uri.IsHexDigit) &&
			CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual));
	}

	public static bool IsApproved(string? installRoot, string? approvedExecutablePath, string? expectedHash,
		string? expectedThumbprint, string? executablePath, string? observedHash, string? observedThumbprint)
	{
		var approved = NormalizeAbsolutePath(approvedExecutablePath, directory: false);
		var observed = NormalizeAbsolutePath(executablePath, directory: false);
		return IsWithinInstallRoot(installRoot, executablePath) && approved is not null && observed is not null &&
			string.Equals(approved, observed, StringComparison.OrdinalIgnoreCase) &&
			Sha256Matches(expectedHash, observedHash) && PublisherMatches(expectedThumbprint, observedThumbprint);
	}
}
