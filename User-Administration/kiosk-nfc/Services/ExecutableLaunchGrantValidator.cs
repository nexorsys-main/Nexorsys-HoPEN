using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Nexorsys.Agent.Core;

namespace Nexorsys.NFC.APP.Services;

public static class ExecutableLaunchGrantValidator
{
    public static bool IsValid(AgentLaunchDescriptor grant, Guid expectedSessionId, DateTimeOffset now)
    {
        if (grant.ApplicationSessionId != expectedSessionId || grant.ApplicationId == Guid.Empty ||
            grant.ExpiresAt <= now || grant.ExpiresAt > now.AddSeconds(20) ||
            !Path.IsPathFullyQualified(grant.ExecutablePath) ||
            !string.Equals(Path.GetExtension(grant.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase) ||
            grant.ExecutableSha256.Length != 64 || !grant.ExecutableSha256.All(Uri.IsHexDigit) ||
            grant.PublisherThumbprint.Length != 40 || !grant.PublisherThumbprint.All(Uri.IsHexDigit)) return false;
        try
        {
            var path = Path.GetFullPath(grant.ExecutablePath);
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            if (!string.Equals(path, grant.ExecutablePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
                !roots.Any(root => !string.IsNullOrWhiteSpace(root) && path.StartsWith(
                    Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), Convert.FromHexString(grant.ExecutableSha256))) return false;
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            var actual = new string((certificate.Thumbprint ?? "").Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
            if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(actual),
                System.Text.Encoding.ASCII.GetBytes(grant.PublisherThumbprint.ToUpperInvariant()))) return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));
            return certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime && certificate.NotAfter.ToUniversalTime() > now.UtcDateTime && chain.Build(certificate);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or FormatException or
            CryptographicException or NotSupportedException) { return false; }
    }
}
