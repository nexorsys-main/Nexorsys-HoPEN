using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Nexorsys.Identity.API.Services;

public interface IWorkstationClientCertificateValidator
{
	bool IsTrusted(X509Certificate2 certificate);
}

/// <summary>Validates workstation client certificates against the host OS trust and revocation configuration.</summary>
public sealed class SystemWorkstationClientCertificateValidator : IWorkstationClientCertificateValidator
{
	public bool IsTrusted(X509Certificate2 certificate)
	{
		if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
			return false;
		try
		{
			using var chain = new X509Chain();
			chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
			chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
			chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
			chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
			return chain.Build(certificate);
		}
		catch (CryptographicException) { return false; }
	}
}
