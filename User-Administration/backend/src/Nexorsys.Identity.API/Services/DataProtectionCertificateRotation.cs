using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Nexorsys.Identity.API.Services;

/// <summary>Configures durable Data Protection keys for certificate rollover.</summary>
public static class DataProtectionCertificateRotation
{
    public static IDataProtectionBuilder Configure(IDataProtectionBuilder builder,
        X509Certificate2 activeEncryptionCertificate, IEnumerable<X509Certificate2> decryptionCertificates)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(activeEncryptionCertificate);
        ArgumentNullException.ThrowIfNull(decryptionCertificates);
        if (!activeEncryptionCertificate.HasPrivateKey)
            throw new InvalidOperationException("The active Data Protection certificate must have a private key.");

        var certificates = decryptionCertificates
            .Prepend(activeEncryptionCertificate)
            .DistinctBy(certificate => NormalizeThumbprint(certificate.Thumbprint), StringComparer.Ordinal)
            .ToArray();
        if (certificates.Any(certificate => !certificate.HasPrivateKey))
            throw new InvalidOperationException("Every Data Protection decryption certificate must have a private key.");

        return builder
            .ProtectKeysWithCertificate(activeEncryptionCertificate)
            .UnprotectKeysWithAnyCertificate(certificates);
    }

    private static string NormalizeThumbprint(string? thumbprint) =>
        (thumbprint ?? string.Empty).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}
