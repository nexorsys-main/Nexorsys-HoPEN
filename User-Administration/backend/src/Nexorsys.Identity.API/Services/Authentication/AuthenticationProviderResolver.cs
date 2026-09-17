using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class AuthenticationProviderResolver
	{
		private readonly IEnumerable<IAuthenticationProvider> _providers;
		private readonly AppDbContext _dbContext;
		private readonly ILogger<AuthenticationProviderResolver> _logger;
		private readonly IOrganizationContext _organization;

		public AuthenticationProviderResolver(
			IEnumerable<IAuthenticationProvider> providers,
			AppDbContext dbContext,
			ILogger<AuthenticationProviderResolver> logger,
			IOrganizationContext organization)
		{
			_providers = providers;
			_dbContext = dbContext;
			_logger = logger;
			_organization = organization;
		}

		public async Task<AuthResult> AuthenticateAndLogAsync(AuthContext context)
		{
			if (!_organization.OrganizationId.HasValue || _organization.OrganizationId.Value == Guid.Empty)
				return new AuthResult { IsSuccess = false, ErrorMessage = "Organization context is invalid." };
			var organizationId = _organization.OrganizationId.Value;
			// Deterministically sort by priority (1 is highest priority)
			var sortedProviders = _providers.OrderBy(p => p.Priority).ToList();

			_logger.LogInformation("Available Providers: {Providers}",
				string.Join(", ", sortedProviders.Select(p => p.ProviderType)));

			IAuthenticationProvider? selectedProvider = null;

			// Simple deterministic selection based on risk context / availability
			// In a real scenario, this would evaluate context.DeviceCertificateThumbprint, policies, etc.
			if (!string.IsNullOrEmpty(context.BadgeUid))
			{
				selectedProvider = sortedProviders.FirstOrDefault(p => p.ProviderType == "NFC");
			}
			else if (!string.IsNullOrEmpty(context.DeviceCertificateThumbprint))
			{
				selectedProvider = sortedProviders.FirstOrDefault(p => p.ProviderType == "Windows");
			}
			else
			{
				selectedProvider = sortedProviders.FirstOrDefault(p => p.ProviderType == "ActiveDirectory");
			}

			if (selectedProvider == null)
			{
				return new AuthResult { IsSuccess = false, ErrorMessage = "No suitable provider found." };
			}

			_logger.LogInformation("Selected Provider: {Provider}", selectedProvider.ProviderType);

			// Execute auth
			var result = await selectedProvider.AuthenticateAsync(context);

			// Log the authentication event as the single source of truth
			var authEvent = new AuthenticationEvent
			{
				Id = Guid.NewGuid(),
				Timestamp = DateTime.UtcNow,
				ProviderType = selectedProvider.ProviderType,
				EventType = "AuthenticationAttempt",
				Result = result.IsSuccess ? "Success" : "Failure",
				OrganizationId = organizationId,
				UserId = result.IsSuccess && Guid.TryParse(result.Profile?.SubjectId, out var userId) ? userId : null,
			};

			_dbContext.AuthenticationEvents.Add(authEvent);
			await _dbContext.SaveChangesAsync();

			return result;
		}
	}
}
