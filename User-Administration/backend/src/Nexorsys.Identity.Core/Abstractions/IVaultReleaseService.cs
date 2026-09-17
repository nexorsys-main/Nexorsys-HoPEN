namespace Nexorsys.Identity.Core.Abstractions;

public interface IVaultReleaseService
{
	Task<VaultReleaseTicket?> CreateGrantAsync(AgentVaultReleaseRequest request, Guid workstationId, Guid organizationId, string agentCertificateThumbprint, CancellationToken cancellationToken);
	Task<VaultReleasedCredential?> RedeemGrantAsync(string grantToken, Guid workstationId, Guid organizationId, string agentCertificateThumbprint, CancellationToken cancellationToken);
}

public sealed record AgentVaultReleaseRequest(Guid UserSessionId, Guid ApplicationSessionId, Guid ApplicationId, Guid VaultEntryId,
	Guid? WindowsBindingId = null, int? WindowsSessionId = null);
public sealed record VaultReleaseTicket(Guid GrantId, string RedemptionToken, DateTime ExpiresAt);
public sealed record VaultReleasedCredential(Guid GrantId, Guid ApplicationId, string Username, string Secret);
