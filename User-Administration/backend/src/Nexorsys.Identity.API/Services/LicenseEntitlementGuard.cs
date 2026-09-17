using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services;

public sealed record EntitlementDecision(bool Allowed, string Code);

public interface ILicenseEntitlementGuard
{
    Task<EntitlementDecision> CanAddUserAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<EntitlementDecision> CanAddWorkstationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<EntitlementDecision> HasFeatureAsync(Guid organizationId, string feature, CancellationToken cancellationToken = default);
}

/// <summary>Central server-side enforcement of locally activated, signed-license entitlements.</summary>
public sealed class LicenseEntitlementGuard(AppDbContext db, IConfiguration configuration, TimeProvider timeProvider,
    SignedLicenseRevocationSnapshotStore? snapshotStore = null, IHttpContextAccessor? httpContextAccessor = null) : ILicenseEntitlementGuard
{
    public async Task<EntitlementDecision> CanAddUserAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (IsDevelopmentOwner() || IsDevelopmentKioskClient()) return new(true, "DEVELOPMENT_OWNER_BYPASS");
        var claims = await FindVerifiedLicenseAsync(organizationId, cancellationToken);
        if (claims is null) return new(false, "LICENSE_INACTIVE");
        var count = await db.Users.IgnoreQueryFilters().CountAsync(u => u.OrganizationId == organizationId, cancellationToken);
        return count < claims.LicensedUsers ? new(true, "ALLOWED") : new(false, "USER_LIMIT_REACHED");
    }

    public async Task<EntitlementDecision> CanAddWorkstationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (IsDevelopmentOwner() || IsDevelopmentKioskClient()) return new(true, "DEVELOPMENT_OWNER_BYPASS");
        var claims = await FindVerifiedLicenseAsync(organizationId, cancellationToken);
        if (claims is null) return new(false, "LICENSE_INACTIVE");
        var count = await db.Workstations.IgnoreQueryFilters().CountAsync(w => w.OrganizationId == organizationId &&
            w.EnrollmentState != WorkstationLifecycle.Revoked && w.EnrollmentState != WorkstationLifecycle.Decommissioned &&
            w.EnrollmentState != WorkstationLifecycle.Replaced, cancellationToken);
        return count < claims.LicensedWorkstations ? new(true, "ALLOWED") : new(false, "WORKSTATION_LIMIT_REACHED");
    }

    public async Task<EntitlementDecision> HasFeatureAsync(Guid organizationId, string feature, CancellationToken cancellationToken = default)
    {
        if (IsDevelopmentOwner() || IsDevelopmentKioskClient()) return new(true, "DEVELOPMENT_OWNER_BYPASS");
        var claims = await FindVerifiedLicenseAsync(organizationId, cancellationToken);
        if (claims is null) return new(false, "LICENSE_INACTIVE");
        return claims.Modules.Contains(feature, StringComparer.OrdinalIgnoreCase)
            ? new(true, "ALLOWED") : new(false, "FEATURE_NOT_LICENSED");
    }

    private bool IsDevelopmentOwner()
    {
        var context = httpContextAccessor?.HttpContext;
        
        // Permanent Owner Bypass for SuperAdmin
        if (context?.User.Identity?.IsAuthenticated == true && context.User.IsInRole("SUPERADMIN"))
        {
            return true;
        }

#if DEBUG
        var enabled = configuration.GetValue<bool>("Licensing:DevelopmentOwnerBypass");
        return enabled && context?.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
#else
        return false;
#endif
    }

    private bool IsDevelopmentKioskClient()
    {
#if DEBUG
        var context = httpContextAccessor?.HttpContext;
        if (!configuration.GetValue<bool>("Licensing:DevelopmentOwnerBypass") ||
            context?.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() != true)
            return false;

        var configuredKey = configuration["KioskApiKey"];
        var suppliedKey = context.Request.Headers["X-Api-Key"].ToString();
        return !string.IsNullOrWhiteSpace(configuredKey) &&
            CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(configuredKey),
                System.Text.Encoding.UTF8.GetBytes(suppliedKey));
#else
        return false;
#endif
    }

    private async Task<LicenseClaims?> FindVerifiedLicenseAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) return null;
        var license = await db.OrganizationLicenses.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (license is null || string.IsNullOrWhiteSpace(license.SignedLicenseToken)) return null;
        var snapshotToken = snapshotStore is null ? configuration["Licensing:RevocationSnapshot"] : snapshotStore.ReadCurrentSnapshot();
        var revocation = SignedLicenseRevocationSnapshotVerifier.Verify(snapshotToken,
            configuration["Licensing:PublicKeyPem"], timeProvider.GetUtcNow());
        if (!revocation.IsValid || revocation.Snapshot is null || revocation.Snapshot.Sequence < license.HighestRevocationSequence) return null;
        var result = SignedLicenseVerifier.Verify(license.SignedLicenseToken, configuration["Licensing:PublicKeyPem"],
            organizationId, configuration["Licensing:Product"] ?? "NexorSys Identity + Kiosk",
            configuration["Licensing:ProductVersion"] ?? "1.5.0", new HashSet<string>(revocation.Snapshot.RevokedLicenseIds, StringComparer.Ordinal),
            timeProvider.GetUtcNow());
        if (!result.IsValid) return null;
        if (revocation.Snapshot.Sequence > license.HighestRevocationSequence)
        {
            // Entitlement checks must not flush unrelated tracked mutations from
            // the caller's unit of work. Persist only this monotonic cache field.
            if (db.Database.IsRelational())
            {
                await db.OrganizationLicenses.IgnoreQueryFilters()
                    .Where(x => x.Id == license.Id && x.HighestRevocationSequence < revocation.Snapshot.Sequence)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.HighestRevocationSequence, revocation.Snapshot.Sequence), cancellationToken);
            }
        }
        return result.Claims;
    }
}
