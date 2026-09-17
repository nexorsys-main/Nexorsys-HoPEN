using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Nexorsys.WindowsAgent;

public sealed record AgentRuntimeConfiguration(Uri IdentityApiBaseUri, string AgentCertificateThumbprint,
	string KioskExecutablePath, string KioskPublisherThumbprint, string KioskExecutableSha256);

public static class AgentCertificateConfiguration
{
	private const string RegistryPath = @"SOFTWARE\NexorSys\Identity\Agent";
	private const string ServiceName = "NexorSysIdentityAgent";

	public static (AgentRuntimeConfiguration Configuration, X509Certificate2 Certificate) Load()
	{
		if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The Agent requires Windows certificate and registry stores.");
		using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
		using var settings = root.OpenSubKey(RegistryPath, writable: false) ?? throw new InvalidOperationException("Agent machine configuration is missing.");
		var apiValue = settings.GetValue("IdentityApiBaseUrl") as string;
		var thumbValue = settings.GetValue("AgentCertificateThumbprint") as string;
		var kioskPublisherValue = settings.GetValue("KioskPublisherThumbprint") as string;
		var kioskExecutablePath = settings.GetValue("KioskExecutablePath") as string;
		var kioskExecutableHash = NormalizeHex(settings.GetValue("KioskExecutableSha256") as string);
		if (!Uri.TryCreate(apiValue, UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttps ||
			apiUri.IsLoopback || !string.IsNullOrEmpty(apiUri.UserInfo) || !string.IsNullOrEmpty(apiUri.Query) || !string.IsNullOrEmpty(apiUri.Fragment))
			throw new InvalidOperationException("Agent Identity API must be a non-loopback HTTPS base URL.");
		var thumbprint = NormalizeThumbprint(thumbValue);
		if (thumbprint.Length != 40 || !thumbprint.All(Uri.IsHexDigit)) throw new InvalidOperationException("Agent certificate thumbprint configuration is invalid.");
		var kioskPublisher = NormalizeHex(kioskPublisherValue);
		if (kioskPublisher.Length != 40 || !kioskPublisher.All(Uri.IsHexDigit)) throw new InvalidOperationException("Trusted Kiosk publisher thumbprint is not provisioned.");
		if (string.IsNullOrWhiteSpace(kioskExecutablePath) || !Path.IsPathFullyQualified(kioskExecutablePath) ||
			!string.Equals(Path.GetFullPath(kioskExecutablePath), kioskExecutablePath, StringComparison.OrdinalIgnoreCase) ||
			kioskExecutableHash.Length != 64 || !kioskExecutableHash.All(Uri.IsHexDigit))
			throw new InvalidOperationException("Trusted Kiosk executable path and SHA-256 must be provisioned.");

		using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
		store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
		var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
			.Cast<X509Certificate2>().Where(c => NormalizeThumbprint(c.Thumbprint) == thumbprint).ToArray();
		if (matches.Length != 1) throw new InvalidOperationException("Expected exactly one Agent certificate in Local Computer\\Personal.");
		var certificate = new X509Certificate2(matches[0]);
		try
		{
			ValidateCertificate(certificate, thumbprint);
			return (new AgentRuntimeConfiguration(new Uri(apiUri.AbsoluteUri.EndsWith('/') ? apiUri.AbsoluteUri : apiUri.AbsoluteUri + '/', UriKind.Absolute),
				thumbprint, kioskExecutablePath, kioskPublisher, kioskExecutableHash), certificate);
		}
		catch { certificate.Dispose(); throw; }
	}

	private static void ValidateCertificate(X509Certificate2 certificate, string expectedThumbprint)
	{
		var now = DateTime.UtcNow;
		if (NormalizeThumbprint(certificate.Thumbprint) != expectedThumbprint || !certificate.HasPrivateKey ||
			certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() <= now)
			throw new CryptographicException("Agent certificate is missing, mismatched, expired, or not yet valid.");
		var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
		if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2"))
			throw new CryptographicException("Agent certificate lacks Client Authentication EKU.");
		using (var chain = new X509Chain())
		{
			chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
			chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
			chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
			chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
			if (!chain.Build(certificate)) throw new CryptographicException("Agent certificate chain/revocation validation failed.");
		}

		var serviceSid = (SecurityIdentifier)new NTAccount($"NT SERVICE\\{ServiceName}").Translate(typeof(SecurityIdentifier));
		var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
		if (certificate.GetRSAPrivateKey() is RSA rsa)
		{
			using (rsa)
			{
				if (rsa is not RSACng cng) throw new CryptographicException("Only an ACL-verifiable CNG Agent key is supported.");
				ValidateCngKeyAcl(cng.Key, serviceSid, systemSid);
				var challenge = RandomNumberGenerator.GetBytes(32);
				var signature = rsa.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
				using var publicKey = certificate.GetRSAPublicKey();
				if (publicKey is null || !publicKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
					throw new CryptographicException("Agent private-key proof failed.");
				CryptographicOperations.ZeroMemory(challenge);
				CryptographicOperations.ZeroMemory(signature);
			}
			return;
		}
		if (certificate.GetECDsaPrivateKey() is ECDsa ecdsa)
		{
			using (ecdsa)
			{
				if (ecdsa is not ECDsaCng cng) throw new CryptographicException("Only an ACL-verifiable CNG Agent key is supported.");
				ValidateCngKeyAcl(cng.Key, serviceSid, systemSid);
				var challenge = RandomNumberGenerator.GetBytes(32);
				var signature = ecdsa.SignData(challenge, HashAlgorithmName.SHA256);
				using var publicKey = certificate.GetECDsaPublicKey();
				if (publicKey is null || !publicKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256))
					throw new CryptographicException("Agent private-key proof failed.");
				CryptographicOperations.ZeroMemory(challenge);
				CryptographicOperations.ZeroMemory(signature);
			}
			return;
		}
		throw new CryptographicException("Unsupported Agent certificate private-key algorithm.");
	}

	private static void ValidateCngKeyAcl(CngKey key, SecurityIdentifier serviceSid, SecurityIdentifier systemSid)
	{
		var descriptorBytes = key.GetProperty("Security Descr", CngPropertyOptions.None).GetValue()
			?? throw new CryptographicException("Agent private-key ACL is unavailable.");
		var descriptor = new RawSecurityDescriptor(descriptorBytes, 0);
		if (descriptor.DiscretionaryAcl is null) throw new CryptographicException("Agent private-key ACL is absent.");
		var serviceAllowed = false;
		foreach (GenericAce ace in descriptor.DiscretionaryAcl)
		{
			if (ace is not QualifiedAce qualified || qualified.AceQualifier != AceQualifier.AccessAllowed) continue;
			if (qualified.SecurityIdentifier == serviceSid) serviceAllowed = true;
			else if (qualified.SecurityIdentifier != systemSid)
				throw new CryptographicException("Agent private key is accessible to an identity other than the service and LocalSystem.");
		}
		if (!serviceAllowed) throw new CryptographicException("Agent service SID is not granted access to its private key.");
	}

	private static string NormalizeThumbprint(string? value) => (value ?? string.Empty).Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
	private static string NormalizeHex(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		var trimmed = value.Trim();
		return trimmed.Any(c => !Uri.IsHexDigit(c) && !char.IsWhiteSpace(c) && c != ':')
			? string.Empty : new string(trimmed.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
	}
}
