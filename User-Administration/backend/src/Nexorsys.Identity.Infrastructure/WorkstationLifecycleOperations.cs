using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Infrastructure;

/// <summary>Atomic state transitions for certificate proofs racing with administrator lifecycle actions.</summary>
public static class WorkstationLifecycleOperations
{
    public static Task<int> TryActivateFromDeviceProofAsync(AppDbContext db, Guid workstationId,
        string normalizedThumbprint, DateTime validatedAtUtc, CancellationToken cancellationToken = default) =>
        db.Workstations.IgnoreQueryFilters()
            .Where(workstation => workstation.Id == workstationId &&
                workstation.EnrollmentState == WorkstationLifecycle.Enrolled && !workstation.IsActive &&
                workstation.DeviceCertificateThumbprint != null &&
                workstation.DeviceCertificateThumbprint.ToUpper() == normalizedThumbprint)
            .ExecuteUpdateAsync(update => update
                .SetProperty(workstation => workstation.EnrollmentState, WorkstationLifecycle.Active)
                .SetProperty(workstation => workstation.IsActive, true)
                .SetProperty(workstation => workstation.EnrolledAt, validatedAtUtc)
                .SetProperty(workstation => workstation.LastValidatedAt, validatedAtUtc), cancellationToken);

    public static Task<int> TryValidateAgentCertificateAsync(AppDbContext db, Guid workstationId,
        string normalizedThumbprint, DateTime validatedAtUtc, CancellationToken cancellationToken = default) =>
        db.Workstations.IgnoreQueryFilters()
            .Where(workstation => workstation.Id == workstationId && workstation.IsActive &&
                workstation.EnrollmentState == WorkstationLifecycle.Active &&
                workstation.AgentCertificateThumbprint != null &&
                workstation.AgentCertificateThumbprint.ToUpper() == normalizedThumbprint)
            .ExecuteUpdateAsync(update => update
                .SetProperty(workstation => workstation.AgentCertificateValidatedAt, validatedAtUtc), cancellationToken);
}
