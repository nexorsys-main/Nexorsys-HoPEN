using Microsoft.Extensions.DependencyInjection;
using Nexorsys.Identity.Infrastructure.Repositories;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Infrastructure
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddInfrastructure(this IServiceCollection services)
		{
			// Add LDAP Service
			services.AddScoped<ILdapService, LdapService>();

			// Add Kiosk Service
		services.AddScoped<IKioskService, KioskService>();
		services.AddDataProtection();
		services.AddScoped<IVaultService, VaultService>();
		services.AddScoped<IVaultReleaseService, VaultReleaseService>();

			// Add audit service
			services.AddHttpContextAccessor();
			services.AddScoped<IAuditService, AuditService>();

			// Add repositories
			services.AddScoped<IUserRepository, UserRepository>();
			services.AddScoped<IGenericRepository<Workflow>, GenericRepository<Workflow>>();

			// Add ANS service
			services.AddScoped<IAnsDeclarationService, AnsDeclarationService>();

			// Add Federation service (PSI / e-CPS extension)
			services.AddScoped<IFederationService, FederationService>();

			// ... other repositories will be added as implemented

			return services;
		}
	}
}
