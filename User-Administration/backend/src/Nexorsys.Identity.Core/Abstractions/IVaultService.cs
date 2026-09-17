using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions;

public interface IVaultService
{
	Task<VaultEntry> StoreAsync(Guid organizationId, Guid applicationId, Guid? siteId, string name, string credentialType, Guid? userId, string username, string secret, Guid actorId, CancellationToken cancellationToken = default);
	Task<bool> CanUseAsync(Guid organizationId, Guid vaultEntryId, Guid userId, Guid workstationId, Guid sessionId, CancellationToken cancellationToken = default);
	Task<IEnumerable<VaultEntry>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);
	Task<VaultEntry?> GetAsync(Guid organizationId, Guid vaultEntryId, CancellationToken cancellationToken = default);
	Task<VaultEntry> RotateAsync(Guid organizationId, Guid vaultEntryId, string newSecret, Guid actorId, CancellationToken cancellationToken = default);
	Task<VaultEntry> RevokeAsync(Guid organizationId, Guid vaultEntryId, Guid actorId, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid organizationId, Guid vaultEntryId, Guid actorId, CancellationToken cancellationToken = default);
}
