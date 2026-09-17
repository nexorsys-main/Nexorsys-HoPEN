using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Npgsql;

namespace Nexorsys.Identity.API.Controllers;

[ApiController]
[Route("api/agent-trust")]
[Authorize(Policy = "AdminOnly")]
public sealed partial class AgentTrustController(AppDbContext db, IOrganizationContext organization,
	Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard) : ControllerBase
{
	private const string ActiveWindowsIdentityIndexName = "ix_windows_user_identity_bindings_organization_id_workstation_";

	[HttpPost("windows-identities")]
	public async Task<IActionResult> BindWindowsIdentity([FromBody] BindWindowsIdentityRequest request, CancellationToken ct)
	{
		if (!organization.OrganizationId.HasValue) return BadRequest("Organization and actor context are required.");
		var actorIdentity = await GetActorAsync(organization.OrganizationId.Value, ct);
		if (!actorIdentity.HasValue) return BadRequest("Organization and actor context are required.");
		var actor = actorIdentity.Value;
		var entitlement = await licenseGuard.HasFeatureAsync(organization.OrganizationId.Value, "kiosk", ct);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		var sid = NormalizeSid(request.WindowsSid);
		if (sid is null) return BadRequest("A valid Windows SID is required.");
		var workstation = await db.Workstations.SingleOrDefaultAsync(w => w.Id == request.WorkstationId &&
			w.OrganizationId == organization.OrganizationId.Value && w.IsActive && w.EnrollmentState == WorkstationLifecycle.Active, ct);
		var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId &&
			u.OrganizationId == organization.OrganizationId.Value && u.IsActive && (!u.LockedUntil.HasValue || u.LockedUntil <= DateTime.UtcNow), ct);
		if (workstation is null || user is null) return NotFound();
		var existing = await db.WindowsUserIdentityBindings.SingleOrDefaultAsync(b => b.OrganizationId == organization.OrganizationId.Value &&
			b.WorkstationId == workstation.Id && b.WindowsSid == sid && b.IsActive, ct);
		if (existing is not null)
		{
			if (existing.UserId == user.Id) return Conflict("This Windows identity is already bound.");
			existing.IsActive = false; existing.RevokedAt = DateTime.UtcNow; existing.RevokedBy = actor;
		}
		var binding = new WindowsUserIdentityBinding
		{
			Id = Guid.NewGuid(), OrganizationId = organization.OrganizationId.Value, WorkstationId = workstation.Id,
			UserId = user.Id, WindowsSid = sid, CreatedAt = DateTime.UtcNow, CreatedBy = actor
		};
		db.WindowsUserIdentityBindings.Add(binding);
		db.AuditLogs.Add(CreateAudit(organization.OrganizationId.Value, actor,
			"Windows identity binding created", "WindowsUserIdentityBinding", binding.Id,
			existing is null ? null : $"ReplacedBindingId={existing.Id}; SID-SHA256={SidDigest(sid)}",
			$"WorkstationId={workstation.Id}; UserId={user.Id}; SID-SHA256={SidDigest(sid)}"));
		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException ex) when (IsActiveWindowsIdentityConflict(ex))
		{
			return Conflict("This Windows identity is already bound to this workstation.");
		}
		return Created("", new { binding.Id, binding.OrganizationId, binding.WorkstationId, binding.UserId, binding.IsActive });
	}

	[HttpDelete("windows-identities/{id:guid}")]
	public async Task<IActionResult> RevokeWindowsIdentity(Guid id, CancellationToken ct)
	{
		if (!organization.OrganizationId.HasValue) return BadRequest("Organization and actor context are required.");
		var actorIdentity = await GetActorAsync(organization.OrganizationId.Value, ct);
		if (!actorIdentity.HasValue) return BadRequest("Organization and actor context are required.");
		var actor = actorIdentity.Value;
		var binding = await db.WindowsUserIdentityBindings.SingleOrDefaultAsync(b => b.Id == id &&
			b.OrganizationId == organization.OrganizationId.Value && b.IsActive, ct);
		if (binding is null) return NotFound();
		binding.IsActive = false; binding.RevokedAt = DateTime.UtcNow; binding.RevokedBy = actor;
		foreach (var live in await db.AgentWindowsSessionBindings.Where(x => x.OrganizationId == binding.OrganizationId &&
			x.WindowsUserIdentityBindingId == id && x.RevokedAt == null).ToListAsync(ct))
			live.RevokedAt = DateTime.UtcNow;
		db.AuditLogs.Add(CreateAudit(binding.OrganizationId, actor,
			"Windows identity binding revoked", "WindowsUserIdentityBinding", id,
			$"IsActive=True; SID-SHA256={SidDigest(binding.WindowsSid)}", "IsActive=False"));
		await db.SaveChangesAsync(ct);
		return NoContent();
	}

	[HttpPut("applications/{id:guid}/registration")]
	public async Task<IActionResult> RegisterApplication(Guid id, [FromBody] RegisterAgentApplicationRequest request, CancellationToken ct)
	{
		if (!organization.OrganizationId.HasValue) return BadRequest("Organization and actor context are required.");
		var actorIdentity = await GetActorAsync(organization.OrganizationId.Value, ct);
		if (!actorIdentity.HasValue) return BadRequest("Organization and actor context are required.");
		var actor = actorIdentity.Value;
		var entitlement = await licenseGuard.HasFeatureAsync(organization.OrganizationId.Value, "kiosk", ct);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		var root = NormalizeWindowsRoot(request.InstallRoot);
		var executable = WindowsApplicationIdentityPolicy.NormalizeAbsolutePath(request.ExecutablePath, directory: false);
		var publisher = WorkstationLifecycle.NormalizeThumbprint(request.PublisherThumbprint ?? string.Empty);
		var sha256 = (request.ExecutableSha256 ?? string.Empty).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
		if (root is null || executable is null || !WindowsApplicationIdentityPolicy.IsWithinInstallRoot(root, executable) ||
			publisher.Length != 40 || !publisher.All(Uri.IsHexDigit) || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
			return BadRequest("A canonical executable path under its install root, SHA-256, and X.509 publisher thumbprint are required.");
		if (!string.Equals(request.CredentialDeliveryMechanism, "none", StringComparison.Ordinal))
			return BadRequest("No credential-delivery adapter is approved in this release.");
		var app = await db.Applications.SingleOrDefaultAsync(a => a.Id == id && a.OrganizationId == organization.OrganizationId.Value, ct);
		if (app is null) return NotFound();
		var oldValues = $"InstallRoot={app.ApprovedInstallRoot}; Executable={app.ApprovedExecutablePath}; " +
			$"SHA256={app.ExpectedExecutableSha256}; Publisher={app.ExpectedPublisherThumbprint}; Delivery={app.CredentialDeliveryMechanism}";
		app.ApprovedInstallRoot = root;
		app.ApprovedExecutablePath = executable;
		app.ExpectedPublisherThumbprint = publisher;
		app.ExpectedExecutableSha256 = sha256;
		app.CredentialDeliveryMechanism = "none";
		app.AgentRegistrationUpdatedAt = DateTime.UtcNow;
		app.AgentRegistrationUpdatedBy = actor;
		db.AuditLogs.Add(CreateAudit(organization.OrganizationId.Value, actor, "Agent application policy registered",
			"Application", app.Id, oldValues,
			$"InstallRoot={root}; Executable={executable}; SHA256={sha256}; Publisher={publisher}; Delivery=none"));
		await db.SaveChangesAsync(ct);
		return Ok(new { app.Id, app.OrganizationId, app.ApprovedInstallRoot, app.ApprovedExecutablePath, app.ExpectedPublisherThumbprint, app.ExpectedExecutableSha256, app.CredentialDeliveryMechanism });
	}

	private AuditLog CreateAudit(Guid organizationId, Guid actor, string action, string resourceType, Guid resourceId,
		string? oldValues, string? newValues) => new()
	{
		Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actor,
		Action = action, ResourceType = resourceType, ResourceId = resourceId,
		OldValues = AuditRedactor.Redact(oldValues), NewValues = AuditRedactor.Redact(newValues),
		IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
		UserAgent = "api", CreatedAt = DateTime.UtcNow
	};

	private async Task<Guid?> GetActorAsync(Guid organizationId, CancellationToken ct)
	{
		if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor == Guid.Empty)
			return null;

		var trackedUser = db.Users.Local.FirstOrDefault(user => user.Id == actor);
		if (trackedUser is not null)
			return trackedUser.OrganizationId == organizationId ? actor : null;

		var persistedOrganization = await db.Users.IgnoreQueryFilters().AsNoTracking()
			.Where(user => user.Id == actor)
			.Select(user => (Guid?)user.OrganizationId)
			.SingleOrDefaultAsync(ct);
		return persistedOrganization.HasValue && persistedOrganization.Value != organizationId ? null : actor;
	}
	private static bool IsActiveWindowsIdentityConflict(Exception exception)
	{
		for (Exception? current = exception; current is not null; current = current.InnerException)
			if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
				postgres.ConstraintName == ActiveWindowsIdentityIndexName) return true;
		return false;
	}

	private static string? NormalizeSid(string? value)
	{
		var sid = value?.Trim().ToUpperInvariant();
		return sid is { Length: <= 184 } && SidPattern().IsMatch(sid) ? sid : null;
	}
	private static string? NormalizeWindowsRoot(string? value) => WindowsApplicationIdentityPolicy.NormalizeAbsolutePath(value, directory: true);
	private static string SidDigest(string sid) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid)))[..16];
	[GeneratedRegex("^S-1-(?:[0-9]+-){1,14}[0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex SidPattern();
}

public sealed record BindWindowsIdentityRequest(Guid WorkstationId, Guid UserId, string WindowsSid);
public sealed record RegisterAgentApplicationRequest(string InstallRoot, string ExecutablePath, string ExecutableSha256, string PublisherThumbprint, string CredentialDeliveryMechanism);
