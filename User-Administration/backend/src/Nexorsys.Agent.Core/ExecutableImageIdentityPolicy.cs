using System.IO;
using System.Linq;

namespace Nexorsys.Agent.Core;

/// <summary>Exact executable allowlist comparison for an OS-observed process image.</summary>
public static class ExecutableImageIdentityPolicy
{
    public static bool IsAllowed(string? expectedPath, string? expectedPublisherThumbprint, string? expectedSha256,
        string? observedPath, string? observedPublisherThumbprint, string? observedSha256, bool signatureTrusted)
    {
        if (!signatureTrusted || string.IsNullOrWhiteSpace(expectedPath) || string.IsNullOrWhiteSpace(observedPath) ||
            !Path.IsPathFullyQualified(expectedPath) || !Path.IsPathFullyQualified(observedPath) ||
            !TryNormalizeHex(expectedPublisherThumbprint, 40, out var expectedPublisher) ||
            !TryNormalizeHex(observedPublisherThumbprint, 40, out var observedPublisher) ||
            !TryNormalizeHex(expectedSha256, 64, out var expectedHash) ||
            !TryNormalizeHex(observedSha256, 64, out var observedHash)) return false;

        try
        {
            return string.Equals(Path.GetFullPath(expectedPath), Path.GetFullPath(observedPath), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(expectedPublisher, observedPublisher, StringComparison.Ordinal) &&
                string.Equals(expectedHash, observedHash, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryNormalizeHex(string? value, int expectedLength, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var candidate = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (candidate.Length != expectedLength || candidate.Length != value.Count(c => !char.IsWhiteSpace(c) && c != ':')) return false;
        normalized = candidate;
        return true;
    }
}
