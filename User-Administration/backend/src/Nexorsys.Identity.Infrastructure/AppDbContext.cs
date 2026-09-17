using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using System.Diagnostics;

namespace Nexorsys.Identity.Infrastructure
{
	public class AppDbContext : DbContext
	{
		private readonly IOrganizationContext _organizationContext;
		internal Guid? CurrentOrganizationId => _organizationContext.OrganizationId;

		public AppDbContext(DbContextOptions<AppDbContext> options, IOrganizationContext organizationContext) : base(options)
		{
			_organizationContext = organizationContext;
		}

		public DbSet<User> Users { get; set; } = null!;
		public DbSet<UserPin> UserPins { get; set; } = null!;
		public DbSet<Application> Applications { get; set; } = null!;
		public DbSet<UserPermission> UserPermissions { get; set; } = null!;
		public DbSet<AuditLog> AuditLogs { get; set; } = null!;
		public DbSet<Workflow> Workflows { get; set; } = null!;
		public DbSet<KioskSession> KioskSessions { get; set; } = null!;
		public DbSet<AnsDelegationLog> AnsDelegationLogs { get; set; } = null!;

		// Federation Layer — PSI / e-CPS Extension
		public DbSet<IdentityProvider> IdentityProviders { get; set; } = null!;
		public DbSet<FederationToken> FederationTokens { get; set; } = null!;
		public DbSet<MigrationPhase> MigrationPhases { get; set; } = null!;
		public DbSet<AuthenticationPolicy> AuthenticationPolicies { get; set; } = null!;
		public DbSet<UserDevice> UserDevices { get; set; } = null!;

		// Enterprise Modules
		public DbSet<Workstation> Workstations { get; set; } = null!;
		public DbSet<UserWorkstation> UserWorkstations { get; set; } = null!;
		public DbSet<CredentialProviderFleet> CredentialProviderFleets { get; set; } = null!;
		public DbSet<UserSession> UserSessions { get; set; } = null!;
		public DbSet<WorkstationSession> WorkstationSessions { get; set; } = null!;
		public DbSet<SessionEvent> SessionEvents { get; set; } = null!;
		public DbSet<PinResetRequest> PinResetRequests { get; set; } = null!;
		public DbSet<AuthenticationEvent> AuthenticationEvents { get; set; } = null!;
		public DbSet<OrganizationLicense> OrganizationLicenses { get; set; } = null!;
		public DbSet<FeatureFlag> FeatureFlags { get; set; } = null!;
		public DbSet<DepartmentPolicy> DepartmentPolicies { get; set; } = null!;
		public DbSet<ApplicationSession> ApplicationSessions { get; set; } = null!;
		public DbSet<Organization> Organizations { get; set; } = null!;
		public DbSet<GlobalSettingsOperation> GlobalSettingsOperations { get; set; } = null!;
		public DbSet<Site> Sites { get; set; } = null!;
		public DbSet<SecurityGroup> SecurityGroups { get; set; } = null!;
		public DbSet<RoleDefinition> RoleDefinitions { get; set; } = null!;
		public DbSet<PermissionDefinition> PermissionDefinitions { get; set; } = null!;
		public DbSet<RolePermission> RolePermissions { get; set; } = null!;
		public DbSet<UserGroupMembership> UserGroupMemberships { get; set; } = null!;
		public DbSet<GroupRoleMembership> GroupRoleMemberships { get; set; } = null!;
		public DbSet<UserRoleMembership> UserRoleMemberships { get; set; } = null!;
		public DbSet<VaultEntry> VaultEntries { get; set; } = null!;
		public DbSet<VaultReleaseGrant> VaultReleaseGrants { get; set; } = null!;
		public DbSet<WindowsUserIdentityBinding> WindowsUserIdentityBindings { get; set; } = null!;
		public DbSet<AgentWindowsSessionBinding> AgentWindowsSessionBindings { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Organization-owned data is invisible unless the request carries an
			// organization claim or an explicit system-administrator role. Existing
			// migration/bootstrap code must use IgnoreQueryFilters intentionally.
			ApplyOrganizationFilter<User>(modelBuilder);
			ApplyOrganizationFilter<UserPin>(modelBuilder);
			ApplyOrganizationFilter<Application>(modelBuilder);
			ApplyOrganizationFilter<UserPermission>(modelBuilder);
			ApplyOrganizationFilter<AuditLog>(modelBuilder);
			ApplyOrganizationFilter<Workflow>(modelBuilder);
			ApplyOrganizationFilter<KioskSession>(modelBuilder);
			ApplyOrganizationFilter<AnsDelegationLog>(modelBuilder);
			ApplyOrganizationFilter<IdentityProvider>(modelBuilder);
			ApplyOrganizationFilter<FederationToken>(modelBuilder);
			ApplyOrganizationFilter<MigrationPhase>(modelBuilder);
			ApplyOrganizationFilter<AuthenticationPolicy>(modelBuilder);
			ApplyOrganizationFilter<UserDevice>(modelBuilder);
			ApplyOrganizationFilter<Workstation>(modelBuilder);
			ApplyOrganizationFilter<UserWorkstation>(modelBuilder);
			ApplyOrganizationFilter<CredentialProviderFleet>(modelBuilder);
			ApplyOrganizationFilter<UserSession>(modelBuilder);
			ApplyOrganizationFilter<WorkstationSession>(modelBuilder);
			ApplyOrganizationFilter<SessionEvent>(modelBuilder);
			ApplyOrganizationFilter<PinResetRequest>(modelBuilder);
			ApplyOrganizationFilter<AuthenticationEvent>(modelBuilder);
			ApplyOrganizationFilter<OrganizationLicense>(modelBuilder);
			ApplyOrganizationFilter<FeatureFlag>(modelBuilder);
			ApplyOrganizationFilter<DepartmentPolicy>(modelBuilder);
			ApplyOrganizationFilter<ApplicationSession>(modelBuilder);
			ApplyOrganizationFilter<Site>(modelBuilder);
			ApplyOrganizationFilter<SecurityGroup>(modelBuilder);
			ApplyOrganizationFilter<RoleDefinition>(modelBuilder);
			ApplyOrganizationFilter<RolePermission>(modelBuilder);
			ApplyOrganizationFilter<UserGroupMembership>(modelBuilder);
			ApplyOrganizationFilter<GroupRoleMembership>(modelBuilder);
			ApplyOrganizationFilter<UserRoleMembership>(modelBuilder);
			ApplyOrganizationFilter<VaultEntry>(modelBuilder);
			ApplyOrganizationFilter<VaultReleaseGrant>(modelBuilder);
			ApplyOrganizationFilter<WindowsUserIdentityBinding>(modelBuilder);
			ApplyOrganizationFilter<AgentWindowsSessionBinding>(modelBuilder);

			// Global naming convention: snake_case for tables and columns
			foreach (var entity in modelBuilder.Model.GetEntityTypes())
			{
				// Map table names
				var tableName = entity.GetTableName();
				if (tableName != null)
				{
					entity.SetTableName(string.Concat(tableName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower());
				}

				// Map column names
				foreach (var property in entity.GetProperties())
				{
					if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
					{
						property.SetColumnType("timestamp");
					}

					var columnName = string.Concat(property.Name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
					property.SetColumnName(columnName);
				}
			}

			// Force lowercase table names to match PostgreSQL schema

			// Configure User entity
			modelBuilder.Entity<User>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.AdGuid).IsUnique();
				entity.HasIndex(e => e.BadgeUid);
				entity.HasIndex(e => e.Email);
			});

			// Configure UserPin entity
			modelBuilder.Entity<UserPin>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.BadgeUid).IsUnique();
				entity.Property(e => e.BadgeUid).HasMaxLength(255);
				entity.HasOne(e => e.User)
					  .WithMany(u => u.UserPins)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.CreatedByUser)
					  .WithMany()
					  .HasForeignKey(e => e.CreatedBy);
			});

			// Configure Application entity
			modelBuilder.Entity<Application>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.ClientId).IsUnique();
			});

			// Configure UserPermission entity
			modelBuilder.Entity<UserPermission>(entity =>
			{
				entity.HasKey(e => e.Id);

				entity.HasOne(e => e.User)
					  .WithMany(u => u.UserPermissions)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Application)
					  .WithMany(a => a.UserPermissions)
					  .HasForeignKey(e => e.ApplicationId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.GrantedByUser)
					  .WithMany()
					  .HasForeignKey(e => e.GrantedBy);
			});

			// Configure AuditLog entity
			modelBuilder.Entity<AuditLog>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.CorrelationId).HasMaxLength(128);
				entity.HasOne(e => e.User)
					  .WithMany()
					  .HasForeignKey(e => e.UserId);
			});

			// Configure Workflow entity
			modelBuilder.Entity<Workflow>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.User)
					  .WithMany(u => u.Workflows)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.AssignedToUser)
					  .WithMany()
					  .HasForeignKey(e => e.AssignedTo);

				entity.HasOne(e => e.AssignedByUser)
					  .WithMany()
					  .HasForeignKey(e => e.AssignedBy);

				entity.Property(e => e.FormData).HasColumnType("jsonb");
			});

			// Configure KioskSession entity
			modelBuilder.Entity<KioskSession>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.User)
					  .WithMany(u => u.KioskSessions)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
			});

			// Configure AnsDelegationLog entity
			modelBuilder.Entity<AnsDelegationLog>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.User)
					  .WithMany()
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.AdminUser)
					  .WithMany()
					  .HasForeignKey(e => e.AdminUserId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			// ═══════════════════════════════════════════════════════
			// FEDERATION LAYER CONFIGURATIONS
			// ═══════════════════════════════════════════════════════

			modelBuilder.Entity<IdentityProvider>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.ProviderType).IsUnique();
				entity.Property(e => e.ConfigurationJson).HasColumnType("jsonb");
			});

			modelBuilder.Entity<FederationToken>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => new { e.UserId, e.ProviderType });
				entity.HasOne(e => e.User)
					  .WithMany()
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<MigrationPhase>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.IsCurrentPhase);
				entity.Property(e => e.ActiveProvidersJson).HasColumnType("jsonb");
			});

			modelBuilder.Entity<AuthenticationPolicy>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.Provider)
					  .WithMany()
					  .HasForeignKey(e => e.ProviderId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			// MIE Device Registry
			modelBuilder.Entity<UserDevice>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => new { e.UserId, e.DeviceType });
				entity.HasIndex(e => e.DeviceIdentifier);
				entity.HasIndex(e => new { e.OrganizationId, e.DeviceType, e.DeviceIdentifier })
					.IsUnique()
					.HasDatabaseName("ux_user_devices_org_type_identifier_active")
					.HasFilter("status <> 'revoked'");
				entity.HasOne(e => e.User)
					  .WithMany(u => u.Devices)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.RegisteredByUser)
					  .WithMany()
					  .HasForeignKey(e => e.RegisteredBy)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			// ═══════════════════════════════════════════════════════
			// ENTERPRISE MODULES CONFIGURATIONS
			// ═══════════════════════════════════════════════════════

			modelBuilder.Entity<Workstation>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => new { e.OrganizationId, e.Hostname }).IsUnique();
				entity.HasIndex(e => e.DeviceCertificateThumbprint)
					.IsUnique()
					.HasFilter("device_certificate_thumbprint IS NOT NULL");
				entity.HasIndex(e => e.AgentCertificateThumbprint)
					.IsUnique()
					.HasFilter("agent_certificate_thumbprint IS NOT NULL");
			});

			modelBuilder.Entity<VaultReleaseGrant>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.TokenHash).IsUnique();
				entity.HasIndex(e => new { e.OrganizationId, e.WorkstationId, e.ExpiresAt });
			});

			modelBuilder.Entity<UserWorkstation>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.WorkstationId });
				entity.HasOne(e => e.User)
					  .WithMany(u => u.UserWorkstations)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.Workstation)
					  .WithMany(w => w.UserWorkstations)
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<CredentialProviderFleet>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.Workstation)
					  .WithOne(w => w.CredentialProviderFleet)
					  .HasForeignKey<CredentialProviderFleet>(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<UserSession>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.User)
					  .WithMany(u => u.UserSessions)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.Workstation)
					  .WithMany()
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<WorkstationSession>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.Workstation)
					  .WithMany()
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.CurrentUser)
					  .WithMany()
					  .HasForeignKey(e => e.CurrentUserId)
					  .OnDelete(DeleteBehavior.SetNull);
				entity.HasOne(e => e.Session)
					  .WithMany()
					  .HasForeignKey(e => e.SessionId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<SessionEvent>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.Session)
					  .WithMany(s => s.SessionEvents)
					  .HasForeignKey(e => e.SessionId)
					  .OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<PinResetRequest>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.User)
					  .WithMany()
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.ApprovedByUser)
					  .WithMany()
					  .HasForeignKey(e => e.ApprovedBy)
					  .OnDelete(DeleteBehavior.SetNull);
				entity.HasOne(e => e.Workstation)
					  .WithMany()
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<AuthenticationEvent>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => new { e.Timestamp, e.UserId, e.WorkstationId });

				entity.HasOne(e => e.User)
					  .WithMany(u => u.AuthenticationEvents)
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.SetNull);
				entity.HasOne(e => e.Workstation)
					  .WithMany()
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<OrganizationLicense>(entity =>
			{
				entity.HasKey(e => e.Id);
			});

			modelBuilder.Entity<FeatureFlag>(entity =>
			{
				entity.HasKey(e => e.Id);
			});

			modelBuilder.Entity<DepartmentPolicy>(entity =>
			{
				entity.HasKey(e => e.Id);
			});

			modelBuilder.Entity<ApplicationSession>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => new { e.UserId, e.ApplicationName });
				entity.HasIndex(e => e.UserSessionId);
				entity.HasIndex(e => e.ApplicationId);

				entity.HasOne(e => e.User)
					  .WithMany()
					  .HasForeignKey(e => e.UserId)
					  .OnDelete(DeleteBehavior.Cascade);
				entity.HasOne(e => e.UserSession)
					  .WithMany()
					  .HasForeignKey(e => e.UserSessionId)
					  .OnDelete(DeleteBehavior.SetNull);
				entity.HasOne(e => e.Application)
					  .WithMany()
					  .HasForeignKey(e => e.ApplicationId)
					  .OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.Workstation)
					  .WithMany()
					  .HasForeignKey(e => e.WorkstationId)
					  .OnDelete(DeleteBehavior.SetNull);
			});
			modelBuilder.Entity<Application>().Property(x => x.CredentialDeliveryMechanism).HasDefaultValue("none");

			modelBuilder.Entity<Organization>().HasKey(x => x.Id);
			modelBuilder.Entity<Organization>().HasIndex(x => x.Slug).IsUnique();
			modelBuilder.Entity<Site>().HasKey(x => x.Id);
			modelBuilder.Entity<Site>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
			modelBuilder.Entity<SecurityGroup>().HasKey(x => x.Id);
			modelBuilder.Entity<SecurityGroup>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
			modelBuilder.Entity<RoleDefinition>().HasKey(x => x.Id);
			modelBuilder.Entity<RoleDefinition>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
			modelBuilder.Entity<PermissionDefinition>().HasKey(x => x.Id);
			modelBuilder.Entity<PermissionDefinition>().HasIndex(x => x.Key).IsUnique();
			modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
			modelBuilder.Entity<UserGroupMembership>().HasKey(x => new { x.UserId, x.GroupId });
			modelBuilder.Entity<GroupRoleMembership>().HasKey(x => new { x.GroupId, x.RoleId });
			modelBuilder.Entity<UserRoleMembership>().HasKey(x => new { x.UserId, x.RoleId });
			modelBuilder.Entity<VaultEntry>().HasKey(x => x.Id);
			modelBuilder.Entity<VaultEntry>().Property(x => x.EncryptedSecret).HasColumnType("bytea");
			modelBuilder.Entity<VaultEntry>().Property(x => x.Nonce).HasColumnType("bytea");
			modelBuilder.Entity<VaultEntry>().UseXminAsConcurrencyToken();
			modelBuilder.Entity<WindowsUserIdentityBinding>(entity =>
			{
				entity.HasKey(x => x.Id);
				entity.Property(x => x.WindowsSid).HasMaxLength(184).IsRequired();
				entity.HasIndex(x => new { x.OrganizationId, x.WorkstationId, x.WindowsSid })
					.IsUnique().HasFilter("is_active = true");
			});
			modelBuilder.Entity<AgentWindowsSessionBinding>(entity =>
			{
				entity.HasKey(x => x.Id);
				entity.Property(x => x.WindowsSid).HasMaxLength(184).IsRequired();
				entity.HasIndex(x => new { x.OrganizationId, x.WorkstationId, x.WindowsSessionId }).IsUnique()
					.HasFilter("revoked_at IS NULL");
				entity.HasIndex(x => new { x.OrganizationId, x.UserSessionId, x.ExpiresAt });
			});
			modelBuilder.Entity<GlobalSettingsOperation>(entity =>
			{
				entity.HasKey(x => x.Id);
				entity.Property(x => x.Sequence).ValueGeneratedOnAdd();
				entity.Property(x => x.SettingsJson).IsRequired();
				entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
				entity.HasIndex(x => x.Sequence).IsUnique();
				entity.HasIndex(x => new { x.Status, x.Sequence });
			});
		}

		private void PopulateAuditCorrelationIds()
		{
			var requestCorrelation = Activity.Current?.TraceId.ToHexString();
			foreach (var entry in ChangeTracker.Entries<AuditLog>().Where(entry => entry.State == EntityState.Added))
			{
				// Correlation is always server-generated; never persist a value supplied
				// by a controller, test payload, or other untrusted caller.
				entry.Entity.CorrelationId = requestCorrelation ?? Guid.NewGuid().ToString("N");
			}
		}

		public override int SaveChanges(bool acceptAllChangesOnSuccess)
		{
			PopulateAuditCorrelationIds();
			return base.SaveChanges(acceptAllChangesOnSuccess);
		}

		public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
			CancellationToken cancellationToken = default)
		{
			PopulateAuditCorrelationIds();
			return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		}

		private void ApplyOrganizationFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
		{
			modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
				_organizationContext.IsSystemAdministrator ||
				(_organizationContext.OrganizationId.HasValue &&
				 EF.Property<Guid>(entity, nameof(User.OrganizationId)) == _organizationContext.OrganizationId.Value));
		}
	}
}
