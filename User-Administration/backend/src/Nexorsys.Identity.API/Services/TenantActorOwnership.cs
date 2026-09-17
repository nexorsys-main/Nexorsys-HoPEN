using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services;

/// <summary>
/// Validates an actor claim against the organization of a mutation or audit row.
/// Missing synthetic actors remain allowed for non-HTTP technical workflows; a
/// real user from another organization is always rejected.
/// </summary>
public static class TenantActorOwnership
{
    public static Guid? ParseAndEnsure(AppDbContext db, ClaimsPrincipal? principal, Guid organizationId)
    {
        if (principal is null)
            return null;
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var actorId) || actorId == Guid.Empty)
            return null;

        Ensure(db, actorId, organizationId);
        return actorId;
    }

    public static void Ensure(AppDbContext db, Guid? actorId, Guid organizationId)
    {
        if (!actorId.HasValue || actorId.Value == Guid.Empty)
            return;

        var tracked = db.Users.Local.FirstOrDefault(user => user.Id == actorId.Value);
        if (tracked is not null)
        {
            if (tracked.OrganizationId != organizationId)
                throw new InvalidOperationException("Actor does not belong to the current organization.");
            return;
        }

        var persistedOrganization = db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.Id == actorId.Value)
            .Select(user => (Guid?)user.OrganizationId)
            .SingleOrDefault();
        if (persistedOrganization.HasValue && persistedOrganization.Value != organizationId)
            throw new InvalidOperationException("Actor does not belong to the current organization.");
    }
}
