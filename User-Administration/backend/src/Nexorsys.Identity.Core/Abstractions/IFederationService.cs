using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	/// <summary>
	/// Service interface for managing federation providers, migration phases,
	/// and authentication policies. This is the orchestration layer.
	/// </summary>
	public interface IFederationService
	{
		// Provider Management
		Task<IEnumerable<IdentityProvider>> GetAllProvidersAsync();
		Task<IdentityProvider?> GetProviderAsync(Guid id);
		Task<IdentityProvider> CreateOrUpdateProviderAsync(IdentityProvider provider);
		Task<bool> ToggleProviderAsync(Guid id, bool isEnabled);
		Task<ProviderHealthStatus> CheckProviderHealthAsync(Guid id);

		// Migration Phases
		Task<IEnumerable<MigrationPhase>> GetAllPhasesAsync();
		Task<MigrationPhase?> GetCurrentPhaseAsync();
		Task<bool> ActivatePhaseAsync(Guid phaseId);

		// Authentication Policies
		Task<IEnumerable<AuthenticationPolicy>> GetAllPoliciesAsync();
		Task<AuthenticationPolicy> CreateOrUpdatePolicyAsync(AuthenticationPolicy policy);
		Task<bool> DeletePolicyAsync(Guid id);
		Task<string> GetRequiredAssuranceLevelAsync(string applicationName, string? department);

		// Federation Tokens
		Task<FederationToken> IssueFederationTokenAsync(Guid userId, string providerType, string tokenRef, string? externalSubjectId, string assuranceLevel);
		Task<bool> RevokeFederationTokenAsync(Guid tokenId);

		// Dashboard Statistics
		Task<object> GetFederationStatsAsync();
	}
}
