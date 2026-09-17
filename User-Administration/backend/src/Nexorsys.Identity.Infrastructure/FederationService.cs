using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure
{
	/// <summary>
	/// Federation orchestration service managing identity providers,
	/// migration phases, authentication policies, and federation tokens.
	/// This service does NOT modify any existing authentication logic.
	/// </summary>
	public class FederationService : IFederationService
	{
		private readonly AppDbContext _context;
		private readonly IOrganizationContext _organization;

		public FederationService(AppDbContext context, IOrganizationContext organization)
		{
			_context = context;
			_organization = organization;
		}

		// ═══════════════════════════════════════
		// PROVIDER MANAGEMENT
		// ═══════════════════════════════════════

		public async Task<IEnumerable<IdentityProvider>> GetAllProvidersAsync()
		{
			return await _context.IdentityProviders
				.Where(p => p.OrganizationId == OrganizationId())
				.OrderBy(p => p.Priority)
				.ToListAsync();
		}

		public async Task<IdentityProvider?> GetProviderAsync(Guid id)
		{
			return await _context.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrganizationId());
		}

		public async Task<IdentityProvider> CreateOrUpdateProviderAsync(IdentityProvider provider)
		{
			var organizationId = OrganizationId();
			var existing = await _context.IdentityProviders
				.FirstOrDefaultAsync(p => p.Id == provider.Id && p.OrganizationId == organizationId);

			if (existing == null)
			{
				provider.Id = Guid.NewGuid();
				provider.OrganizationId = organizationId;
				provider.CreatedAt = DateTime.UtcNow;
				provider.UpdatedAt = DateTime.UtcNow;
				await _context.IdentityProviders.AddAsync(provider);
			}
			else
			{
				existing.ProviderName = provider.ProviderName;
				existing.IsEnabled = provider.IsEnabled;
				existing.Priority = provider.Priority;
				existing.ConfigurationJson = provider.ConfigurationJson;
				existing.UpdatedAt = DateTime.UtcNow;
			}

			await _context.SaveChangesAsync();
			return provider;
		}

		public async Task<bool> ToggleProviderAsync(Guid id, bool isEnabled)
		{
			var provider = await _context.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrganizationId());
			if (provider == null) return false;

			provider.IsEnabled = isEnabled;
			provider.UpdatedAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<ProviderHealthStatus> CheckProviderHealthAsync(Guid id)
		{
			var provider = await _context.IdentityProviders.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrganizationId());
			if (provider == null)
				return new ProviderHealthStatus { Status = "offline", Message = "Provider not found" };

			// For NFC and AD providers, they are always "healthy" if enabled
			var status = new ProviderHealthStatus
			{
				Status = provider.IsEnabled ? "healthy" : "offline",
				Message = provider.IsEnabled ? "Provider is active" : "Provider is disabled",
				CheckedAt = DateTime.UtcNow
			};

			provider.HealthStatus = status.Status;
			provider.LastHealthCheck = status.CheckedAt;
			await _context.SaveChangesAsync();

			return status;
		}

		// ═══════════════════════════════════════
		// MIGRATION PHASES
		// ═══════════════════════════════════════

		public async Task<IEnumerable<MigrationPhase>> GetAllPhasesAsync()
		{
			return await _context.MigrationPhases
				.Where(p => p.OrganizationId == OrganizationId())
				.OrderBy(p => p.PhaseName)
				.ToListAsync();
		}

		public async Task<MigrationPhase?> GetCurrentPhaseAsync()
		{
			return await _context.MigrationPhases
				.FirstOrDefaultAsync(p => p.IsCurrentPhase && p.OrganizationId == OrganizationId());
		}

		public async Task<bool> ActivatePhaseAsync(Guid phaseId)
		{
			var organizationId = OrganizationId();
			var phase = await _context.MigrationPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.OrganizationId == organizationId);
			if (phase == null) return false;

			// Deactivate all other phases
			var allPhases = await _context.MigrationPhases.Where(p => p.OrganizationId == organizationId).ToListAsync();
			foreach (var p in allPhases)
			{
				p.IsCurrentPhase = false;
			}

			phase.IsCurrentPhase = true;
			phase.ActivatedAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();
			return true;
		}

		// ═══════════════════════════════════════
		// AUTHENTICATION POLICIES
		// ═══════════════════════════════════════

		public async Task<IEnumerable<AuthenticationPolicy>> GetAllPoliciesAsync()
		{
			return await _context.AuthenticationPolicies
				.Where(p => p.OrganizationId == OrganizationId())
				.Include(p => p.Provider)
				.OrderBy(p => p.PolicyName)
				.ToListAsync();
		}

		public async Task<AuthenticationPolicy> CreateOrUpdatePolicyAsync(AuthenticationPolicy policy)
		{
			var organizationId = OrganizationId();
			if (policy.ProviderId.HasValue && !await _context.IdentityProviders.IgnoreQueryFilters()
				.AnyAsync(p => p.Id == policy.ProviderId.Value && p.OrganizationId == organizationId))
				throw new InvalidOperationException("Authentication policy provider is unavailable in the current organization.");
			var existing = await _context.AuthenticationPolicies
				.FirstOrDefaultAsync(p => p.Id == policy.Id && p.OrganizationId == organizationId);

			if (existing == null)
			{
				policy.Id = Guid.NewGuid();
				policy.OrganizationId = organizationId;
				policy.CreatedAt = DateTime.UtcNow;
				policy.UpdatedAt = DateTime.UtcNow;
				await _context.AuthenticationPolicies.AddAsync(policy);
			}
			else
			{
				existing.PolicyName = policy.PolicyName;
				existing.AssuranceLevel = policy.AssuranceLevel;
				existing.ProviderId = policy.ProviderId;
				existing.TargetGroup = policy.TargetGroup;
				existing.TargetApplication = policy.TargetApplication;
				existing.IsActive = policy.IsActive;
				existing.UpdatedAt = DateTime.UtcNow;
			}

			await _context.SaveChangesAsync();
			return policy;
		}

		public async Task<bool> DeletePolicyAsync(Guid id)
		{
			var policy = await _context.AuthenticationPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == OrganizationId());
			if (policy == null) return false;

			_context.AuthenticationPolicies.Remove(policy);
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<string> GetRequiredAssuranceLevelAsync(string applicationName, string? department)
		{
			var policy = await _context.AuthenticationPolicies
				.Where(p => p.OrganizationId == OrganizationId() && p.IsActive && p.TargetApplication == applicationName &&
					(!p.ProviderId.HasValue || (p.Provider != null && p.Provider.OrganizationId == OrganizationId())))
				.FirstOrDefaultAsync();

			return policy?.AssuranceLevel ?? "Standard";
		}

		// ═══════════════════════════════════════
		// FEDERATION TOKENS
		// ═══════════════════════════════════════

		public async Task<FederationToken> IssueFederationTokenAsync(
			Guid userId, string providerType, string tokenRef, string? externalSubjectId, string assuranceLevel)
		{
			var organizationId = OrganizationId();
			var userBelongsToOrganization = await _context.Users.IgnoreQueryFilters()
				.AnyAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (!userBelongsToOrganization) throw new InvalidOperationException("Federation token owner is unavailable in the current organization.");
			var token = new FederationToken
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				UserId = userId,
				ProviderType = providerType,
				TokenReference = tokenRef,
				ExternalSubjectId = externalSubjectId,
				AssuranceLevel = assuranceLevel,
				IssuedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddHours(8)
			};

			await _context.FederationTokens.AddAsync(token);
			await _context.SaveChangesAsync();
			return token;
		}

		public async Task<bool> RevokeFederationTokenAsync(Guid tokenId)
		{
			var token = await _context.FederationTokens.FirstOrDefaultAsync(t => t.Id == tokenId && t.OrganizationId == OrganizationId());
			if (token == null) return false;

			token.IsRevoked = true;
			await _context.SaveChangesAsync();
			return true;
		}

		// ═══════════════════════════════════════
		// DASHBOARD STATISTICS
		// ═══════════════════════════════════════

		public async Task<object> GetFederationStatsAsync()
		{
			var organizationId = OrganizationId();
			var providers = await _context.IdentityProviders.Where(p => p.OrganizationId == organizationId).ToListAsync();
			var currentPhase = await GetCurrentPhaseAsync();
			var policies = await _context.AuthenticationPolicies.CountAsync(p => p.OrganizationId == organizationId && p.IsActive);
			var activeTokens = await _context.FederationTokens
				.CountAsync(t => t.OrganizationId == organizationId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

			return new
			{
				TotalProviders = providers.Count,
				ActiveProviders = providers.Count(p => p.IsEnabled),
				HealthyProviders = providers.Count(p => p.HealthStatus == "healthy"),
				CurrentPhase = currentPhase?.PhaseName ?? "Phase1_Operational",
				FederationEnabled = currentPhase?.FederationEnabled ?? false,
				ActivePolicies = policies,
				ActiveFederationTokens = activeTokens,
				Providers = providers.Select(p => new
				{
					p.Id,
					p.ProviderName,
					p.ProviderType,
					p.IsEnabled,
					p.Priority,
					p.HealthStatus,
					p.LastHealthCheck
				})
			};
		}

		private Guid OrganizationId() => _organization.OrganizationId is { } organizationId && organizationId != Guid.Empty
			? organizationId
			: throw new InvalidOperationException("Federation organization context is unavailable.");
	}
}
