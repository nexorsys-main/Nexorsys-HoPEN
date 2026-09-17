using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("NEXORSYS_DATABASE")
			?? throw new InvalidOperationException("NEXORSYS_DATABASE must be supplied for design-time EF operations.");
		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(connectionString)
			.UseSnakeCaseNamingConvention()
			.Options;
		return new AppDbContext(options, new DesignTimeOrganizationContext());
	}

	private sealed class DesignTimeOrganizationContext : IOrganizationContext
	{
		public Guid? OrganizationId => null;
		public bool IsSystemAdministrator => true;
	}
}
