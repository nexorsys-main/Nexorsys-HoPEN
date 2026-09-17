using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Controllers;

[ApiController]
[Route("api/workstations/enrollment")]
[Authorize(Policy = "AdminOnly")]
public sealed class WorkstationEnrollmentController : ControllerBase
{
	private readonly AppDbContext _db;
	private readonly IOrganizationContext _organization;
	private readonly IAuditService _audit;
	private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;
	private readonly Nexorsys.Identity.API.Services.IWorkstationClientCertificateValidator _certificateValidator;
	private readonly IHostEnvironment _environment;

	public WorkstationEnrollmentController(AppDbContext db, IOrganizationContext organization, IAuditService audit,
		Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard,
		Nexorsys.Identity.API.Services.IWorkstationClientCertificateValidator? certificateValidator = null,
		IHostEnvironment? environment = null)
	{
		_db = db;
		_organization = organization;
		_audit = audit;
		_licenseGuard = licenseGuard;
		_certificateValidator = certificateValidator ?? new Nexorsys.Identity.API.Services.SystemWorkstationClientCertificateValidator();
		_environment = environment!;
	}

	[HttpPost("dev-bootstrap")]
	public async Task<IActionResult> DevelopmentBootstrap([FromBody] DevBootstrapWorkstationRequest? request, CancellationToken cancellationToken)
	{
		if (_environment is null || !_environment.IsDevelopment()) return NotFound();
		if (!_organization.OrganizationId.HasValue) return BadRequest("Organization context is required.");
		var feature = await RequireKioskEntitlementAsync(_organization.OrganizationId.Value, cancellationToken);
		if (feature is not null) return feature;
		var hostname = string.IsNullOrWhiteSpace(request?.Hostname) ? Environment.MachineName : request.Hostname.Trim();
		var existing = await _db.Workstations.FirstOrDefaultAsync(w => w.OrganizationId == _organization.OrganizationId.Value && w.Hostname == hostname, cancellationToken);
		if (existing is not null)
		{
			existing.IsActive = true;
			existing.EnrollmentState = WorkstationLifecycle.Active;
			existing.EnrolledAt ??= DateTime.UtcNow;
			await _db.SaveChangesAsync(cancellationToken);
			return Ok(new { existing.Id, existing.Hostname, existing.EnrollmentState, developmentOnly = true });
		}
		var workstation = new Workstation
		{
			Id = Guid.NewGuid(), OrganizationId = _organization.OrganizationId.Value,
			Hostname = hostname, Location = request?.Location?.Trim(), Department = request?.Department?.Trim(),
			IsActive = true, EnrollmentState = WorkstationLifecycle.Active,
			CreatedAt = DateTime.UtcNow, EnrolledAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
		};
		_db.Workstations.Add(workstation);
		_audit.Add("Development workstation bootstrap", "Workstation", workstation.Id, newValues: "ACTIVE (development only)");
		await _db.SaveChangesAsync(cancellationToken);
		return CreatedAtAction(nameof(Get), new { id = workstation.Id }, new { workstation.Id, workstation.Hostname, workstation.EnrollmentState, developmentOnly = true });
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] EnrollWorkstationRequest request, CancellationToken cancellationToken)
	{
		if (!_organization.OrganizationId.HasValue) return BadRequest("Organization context is required.");
		try
		{
			var feature = await RequireKioskEntitlementAsync(_organization.OrganizationId.Value, cancellationToken);
			if (feature is not null) return feature;
			await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
			var entitlement = await _licenseGuard.CanAddWorkstationAsync(_organization.OrganizationId.Value, cancellationToken);
			if (!entitlement.Allowed) return Conflict(new { code = entitlement.Code });
			if (string.IsNullOrWhiteSpace(request.Hostname) || request.Hostname.Length > 255) return BadRequest("A valid hostname is required.");
			var hostname = request.Hostname.Trim();
			var organizationId = _organization.OrganizationId.Value;
			if (await _db.Workstations.AnyAsync(w => w.OrganizationId == organizationId && w.Hostname == hostname, cancellationToken))
				return Conflict("Hostname is already enrolled in this organization.");

			var workstation = new Workstation
			{
				Id = Guid.NewGuid(), OrganizationId = organizationId,
				Hostname = hostname, Location = request.Location?.Trim(), Department = request.Department?.Trim(),
				IsActive = false, EnrollmentState = WorkstationLifecycle.PendingApproval, CreatedAt = DateTime.UtcNow
			};
			_db.Workstations.Add(workstation);
			_audit.Add("Workstation enrollment requested", "Workstation", workstation.Id,
				newValues: WorkstationLifecycle.PendingApproval);
			await _db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return CreatedAtAction(nameof(Get), new { id = workstation.Id }, new { workstation.Id, workstation.Hostname, workstation.EnrollmentState });
		}
		catch (Exception exception) when (Nexorsys.Identity.API.Services.DatabaseConcurrencyErrors.IsSerializationFailure(exception))
		{
			return Conflict(new { code = "CONCURRENT_WRITE_RETRY" });
		}
	}

	[HttpGet("{id:guid}")]
	public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
	{
		if (!_organization.OrganizationId.HasValue) return BadRequest("Organization context is required.");
		var feature = await RequireKioskEntitlementAsync(_organization.OrganizationId.Value, cancellationToken);
		if (feature is not null) return feature;
		var workstation = await _db.Workstations.AsNoTracking()
			.Where(w => w.Id == id && w.OrganizationId == _organization.OrganizationId.Value)
			.Select(w => new { w.Id, w.Hostname, w.EnrollmentState, w.IsActive, w.EnrolledAt, w.ApprovedAt,
				w.LastValidatedAt, w.RevokedAt, w.DecommissionedAt, w.ReplacedByWorkstationId })
			.SingleOrDefaultAsync(cancellationToken);
		return workstation is null ? NotFound() : Ok(workstation);
	}

	/// <summary>Operator approval associates the externally-issued certificate thumbprint; it does not activate trust.</summary>
	[HttpPost("{id:guid}/approve")]
	public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveWorkstationRequest request, CancellationToken cancellationToken)
	{
		if (!_organization.OrganizationId.HasValue) return BadRequest("Organization context is required.");
		var feature = await RequireKioskEntitlementAsync(_organization.OrganizationId.Value, cancellationToken);
		if (feature is not null) return feature;
		var thumbprint = request.CertificateThumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
		if (thumbprint is null || thumbprint.Length != 40 || !thumbprint.All(Uri.IsHexDigit)) return BadRequest("A SHA-1 X.509 thumbprint is required.");
		var workstation = await FindInOrganization(id, cancellationToken);
		if (workstation is null) return NotFound();
		if (workstation.EnrollmentState != WorkstationLifecycle.PendingApproval) return Conflict("Workstation is not awaiting approval.");
		if (await _db.Workstations.IgnoreQueryFilters().AnyAsync(w => w.Id != id && w.DeviceCertificateThumbprint != null &&
			w.DeviceCertificateThumbprint.ToUpper() == thumbprint, cancellationToken))
			return Conflict("This certificate is already bound to another workstation.");

		workstation.DeviceCertificateThumbprint = thumbprint;
		workstation.ApprovedAt = DateTime.UtcNow;
		workstation.EnrollmentState = WorkstationLifecycle.Enrolled;
		workstation.IsActive = false;
		_audit.Add("Workstation enrollment approved; certificate proof pending", "Workstation", id,
			newValues: WorkstationLifecycle.Enrolled);
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new { workstation.Id, workstation.EnrollmentState, proofOfPossessionRequired = true });
	}

	[HttpPost("{id:guid}/agent-certificate/approve")]
	public async Task<IActionResult> ApproveAgentCertificate(Guid id, [FromBody] ApproveWorkstationRequest request, CancellationToken cancellationToken)
	{
		if (!_organization.OrganizationId.HasValue) return BadRequest("Organization context is required.");
		var feature = await RequireKioskEntitlementAsync(_organization.OrganizationId.Value, cancellationToken);
		if (feature is not null) return feature;
		var thumbprint = request.CertificateThumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
		if (thumbprint is null || thumbprint.Length != 40 || !thumbprint.All(Uri.IsHexDigit)) return BadRequest("A SHA-1 X.509 thumbprint is required.");
		var workstation = await FindInOrganization(id, cancellationToken);
		if (workstation is null) return NotFound();
		if (workstation.EnrollmentState != WorkstationLifecycle.Active || !workstation.IsActive) return Conflict("The workstation must be active before Agent certificate enrollment.");
		if (await _db.Workstations.IgnoreQueryFilters().AnyAsync(w => w.Id != id && w.AgentCertificateThumbprint != null &&
			w.AgentCertificateThumbprint.ToUpper() == thumbprint, cancellationToken))
			return Conflict("This Agent certificate is already bound to another workstation.");
		workstation.AgentCertificateThumbprint = thumbprint;
		workstation.AgentCertificateValidatedAt = null;
		_audit.Add("Agent certificate enrollment approved; proof pending", "Workstation", id, newValues: "proof-of-possession required");
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new { workstation.Id, proofOfPossessionRequired = true });
	}

	[AllowAnonymous]
	[HttpPost("{id:guid}/agent-certificate/prove-possession")]
	public async Task<IActionResult> ProveAgentCertificate(Guid id, CancellationToken cancellationToken)
	{
		var certificate = await HttpContext.Connection.GetClientCertificateAsync(cancellationToken);
		if (certificate is null || !_certificateValidator.IsTrusted(certificate)) return Unauthorized();
		var thumbprint = WorkstationLifecycle.NormalizeThumbprint(certificate.Thumbprint ?? string.Empty);
		var organizationId = await _db.Workstations.IgnoreQueryFilters()
			.Where(w => w.Id == id && w.AgentCertificateThumbprint == thumbprint && w.EnrollmentState == WorkstationLifecycle.Active)
			.Select(w => w.OrganizationId).SingleOrDefaultAsync(cancellationToken);
		if (organizationId == Guid.Empty) return Unauthorized();
		var feature = await RequireKioskEntitlementAsync(organizationId, cancellationToken);
		if (feature is not null) return feature;
		await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
		var validatedAt = DateTime.UtcNow;
		var updated = await WorkstationLifecycleOperations.TryValidateAgentCertificateAsync(_db, id, thumbprint, validatedAt, cancellationToken);
		if (updated != 1) return Unauthorized();
		_db.AuditLogs.Add(new AuditLog
		{
			Id = Guid.NewGuid(), OrganizationId = organizationId,
			Action = "Agent certificate proof accepted", ResourceType = "Workstation", ResourceId = id,
			NewValues = "Agent certificate bound", CreatedAt = validatedAt
		});
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return Ok(new { id, agentAuthenticated = true });
	}

	/// <summary>Certificate possession activates a pre-approved enrollment. No thumbprint supplied by the caller is trusted.</summary>
	[AllowAnonymous]
	[HttpPost("{id:guid}/prove-possession")]
	public async Task<IActionResult> ProvePossession(Guid id, CancellationToken cancellationToken)
	{
		var certificate = await HttpContext.Connection.GetClientCertificateAsync(cancellationToken);
		if (certificate is null || !_certificateValidator.IsTrusted(certificate)) return Unauthorized();
		var thumbprint = WorkstationLifecycle.NormalizeThumbprint(certificate.Thumbprint ?? string.Empty);
		var organizationId = await _db.Workstations.IgnoreQueryFilters()
			.Where(w => w.Id == id && w.DeviceCertificateThumbprint == thumbprint && w.EnrollmentState == WorkstationLifecycle.Enrolled)
			.Select(w => w.OrganizationId).SingleOrDefaultAsync(cancellationToken);
		if (organizationId == Guid.Empty) return Unauthorized();
		var feature = await RequireKioskEntitlementAsync(organizationId, cancellationToken);
		if (feature is not null) return feature;
		await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
		var validatedAt = DateTime.UtcNow;
		var updated = await WorkstationLifecycleOperations.TryActivateFromDeviceProofAsync(_db, id, thumbprint, validatedAt, cancellationToken);
		if (updated != 1) return Unauthorized();
		_db.AuditLogs.Add(new AuditLog
		{
			Id = Guid.NewGuid(), OrganizationId = organizationId,
			Action = "Workstation certificate proof accepted", ResourceType = "Workstation",
			ResourceId = id, NewValues = WorkstationLifecycle.Active, CreatedAt = validatedAt
		});
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return Ok(new { id, enrollmentState = WorkstationLifecycle.Active });
	}

	[HttpPost("{id:guid}/revoke")]
	public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
	{
		var workstation = await FindInOrganization(id, cancellationToken);
		if (workstation is null) return NotFound();
		if (IsTerminal(workstation.EnrollmentState)) return Conflict("Workstation is already in a terminal state.");
		workstation.EnrollmentState = WorkstationLifecycle.Revoked;
		workstation.IsActive = false;
		workstation.RevokedAt = DateTime.UtcNow;
		await RevokeSessions(workstation.OrganizationId, id, cancellationToken);
		_audit.Add("Workstation certificate revoked", "Workstation", id, newValues: WorkstationLifecycle.Revoked);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	[HttpPost("{id:guid}/decommission")]
	public async Task<IActionResult> Decommission(Guid id, CancellationToken cancellationToken)
	{
		var workstation = await FindInOrganization(id, cancellationToken);
		if (workstation is null) return NotFound();
		if (IsTerminal(workstation.EnrollmentState)) return Conflict("Workstation is already in a terminal state.");
		workstation.EnrollmentState = WorkstationLifecycle.Decommissioned;
		workstation.IsActive = false;
		workstation.DecommissionedAt = DateTime.UtcNow;
		await RevokeSessions(workstation.OrganizationId, id, cancellationToken);
		_audit.Add("Workstation decommissioned", "Workstation", id, newValues: WorkstationLifecycle.Decommissioned);
		await _db.SaveChangesAsync(cancellationToken);
		return NoContent();
	}

	[HttpPost("{id:guid}/replace/{replacementId:guid}")]
	public async Task<IActionResult> Replace(Guid id, Guid replacementId, CancellationToken cancellationToken)
	{
		var oldDevice = await FindInOrganization(id, cancellationToken);
		var replacement = await FindInOrganization(replacementId, cancellationToken);
		if (oldDevice is null || replacement is null) return NotFound();
		if (oldDevice.EnrollmentState != WorkstationLifecycle.Active || replacement.EnrollmentState != WorkstationLifecycle.PendingApproval)
			return Conflict("Replacement requires an active source and a pending replacement in the same organization.");
		oldDevice.EnrollmentState = WorkstationLifecycle.Replaced;
		oldDevice.IsActive = false;
		oldDevice.ReplacedByWorkstationId = replacement.Id;
		await RevokeSessions(oldDevice.OrganizationId, id, cancellationToken);
		_audit.Add("Workstation replaced", "Workstation", id, newValues: $"ReplacementId={replacement.Id}");
		await _db.SaveChangesAsync(cancellationToken);
		return Ok(new { oldDevice.Id, oldDevice.EnrollmentState, replacementId = replacement.Id });
	}

	private Task<Workstation?> FindInOrganization(Guid id, CancellationToken ct) =>
		_organization.OrganizationId.HasValue
			? _db.Workstations.SingleOrDefaultAsync(w => w.Id == id && w.OrganizationId == _organization.OrganizationId.Value, ct)
			: Task.FromResult<Workstation?>(null);

	private async Task<IActionResult?> RequireKioskEntitlementAsync(Guid organizationId, CancellationToken cancellationToken)
	{
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "kiosk", cancellationToken);
		return entitlement.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
	}

	private async Task RevokeSessions(Guid organizationId, Guid workstationId, CancellationToken ct)
	{
		var sessions = await _db.UserSessions.Where(s => s.OrganizationId == organizationId && s.WorkstationId == workstationId && s.Status == "Active").ToListAsync(ct);
		foreach (var session in sessions) { session.Status = "Revoked"; session.SessionEndedAt = DateTime.UtcNow; }
		var appSessions = await _db.ApplicationSessions.Where(s => s.OrganizationId == organizationId && s.WorkstationId == workstationId && s.EndTime == null).ToListAsync(ct);
		foreach (var session in appSessions) session.EndTime = DateTime.UtcNow;
		var workstationSessions = await _db.WorkstationSessions.Where(s => s.OrganizationId == organizationId && s.WorkstationId == workstationId && s.SessionEndedAt == null).ToListAsync(ct);
		foreach (var session in workstationSessions) session.SessionEndedAt = DateTime.UtcNow;
		var kioskSessions = await _db.KioskSessions.Where(s => s.OrganizationId == organizationId && s.WorkstationId == workstationId && s.ExpiresAt > DateTime.UtcNow).ToListAsync(ct);
		foreach (var session in kioskSessions) session.ExpiresAt = DateTime.UtcNow;
	}

	private static bool IsTerminal(string state) => state is WorkstationLifecycle.Revoked or WorkstationLifecycle.Replaced or WorkstationLifecycle.Decommissioned;

}

public sealed class DevBootstrapWorkstationRequest
{
	public string? Hostname { get; set; }
	public string? Location { get; set; }
	public string? Department { get; set; }
}

public sealed record EnrollWorkstationRequest(string Hostname, string? Department, string? Location);
public sealed record ApproveWorkstationRequest(string CertificateThumbprint);
