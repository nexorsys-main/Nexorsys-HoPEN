using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Agent.Core;
using Npgsql;

namespace Nexorsys.Identity.API.Controllers;

/// <summary>mTLS-only, Agent-certificate-only API. It is intentionally not a browser/user REST surface.</summary>
[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(Nexorsys.Identity.API.Filters.AgentCertificateAuthenticationFilter))]
[Route("api/agent/v1/vault-release")]
public sealed partial class AgentController : ControllerBase
{
	private static readonly bool VaultCredentialReleaseEnabled = false;
	private const string ActiveAgentWindowsSessionIndexName = "ix_agent_windows_session_bindings_organization_id_workstation_";
	private readonly AppDbContext _db;
	private readonly IVaultReleaseService _release;
	private readonly ILogger<AgentController> _logger;
	private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

	public AgentController(AppDbContext db, IVaultReleaseService release, ILogger<AgentController> logger,
		Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
	{
		_db = db;
		_release = release;
		_logger = logger;
		_licenseGuard = licenseGuard;
	}

	[HttpPost("grants")]
	public async Task<IActionResult> CreateGrant([FromBody] AgentVaultReleaseRequest request, CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "no-store";
		var agent = GetAuthenticatedAgent();
		if (agent is null) return Unauthorized();
		if (!VaultCredentialReleaseEnabled)
		{
			await AuditDenial(agent, "AgentVaultReleaseConfigurationDenied", request.VaultEntryId, cancellationToken);
			return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "RELEASE_NOT_CONFIGURED" });
		}
		var ticket = await _release.CreateGrantAsync(request, agent.Id, agent.OrganizationId,
			agent.AgentCertificateThumbprint!, cancellationToken);
		if (ticket is null)
		{
			await AuditDenial(agent, "VaultReleaseGrantDenied", request.VaultEntryId, cancellationToken);
			return Forbid();
		}
		return Ok(ticket);
	}

	[HttpPost("redeem")]
	public async Task<IActionResult> Redeem([FromBody] RedeemGrantRequest request, CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "no-store";
		var agent = GetAuthenticatedAgent();
		if (agent is null) return Unauthorized();
		if (!VaultCredentialReleaseEnabled)
		{
			await AuditDenial(agent, "AgentVaultReleaseConfigurationDenied", null, cancellationToken);
			return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "RELEASE_NOT_CONFIGURED" });
		}
		var released = await _release.RedeemGrantAsync(request.RedemptionToken, agent.Id, agent.OrganizationId,
			agent.AgentCertificateThumbprint!, cancellationToken);
		if (released is null)
		{
			await AuditDenial(agent, "VaultReleaseDeniedOrReplay", null, cancellationToken);
			return Forbid();
		}
		// This is the dedicated mTLS Agent boundary, not a browser or general-purpose REST endpoint.
		// The caller must be the installed Agent with exclusive private-key access.
		return Ok(released);
	}

	/// <summary>Registers a short-lived observation from the authenticated Agent. SID/session are accepted only from Agent mTLS, never from the named-pipe client directly.</summary>
	[HttpPost("windows-sessions/bind")]
	public async Task<IActionResult> BindWindowsSession([FromBody] BindWindowsSessionRequest request, CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "no-store";
		var agent = GetAuthenticatedAgent();
		if (agent is null) return Unauthorized();
		var sid = request.WindowsSid?.Trim().ToUpperInvariant();
		if (sid is null || sid.Length > 184 || !WindowsSidPattern().IsMatch(sid) || request.WindowsSessionId < 0)
			return BadRequest("Windows caller context is malformed.");
		var entitlement = await _licenseGuard.HasFeatureAsync(agent.OrganizationId, "kiosk", cancellationToken);
		if (!entitlement.Allowed) return await DenyBinding(agent, "AgentWindowsSessionBindingNotLicensed", cancellationToken);

		var now = DateTime.UtcNow;
		var identity = await _db.WindowsUserIdentityBindings.IgnoreQueryFilters().SingleOrDefaultAsync(x =>
			x.OrganizationId == agent.OrganizationId && x.WorkstationId == agent.Id && x.WindowsSid == sid && x.IsActive, cancellationToken);
		if (identity is null) return await DenyBinding(agent, "AgentWindowsIdentityBindingDenied", cancellationToken);

		var candidates = await _db.UserSessions.IgnoreQueryFilters().Include(x => x.User).Where(s =>
			s.OrganizationId == agent.OrganizationId && s.WorkstationId == agent.Id && s.UserId == identity.UserId &&
			s.Status == "Active" && s.SessionEndedAt == null && s.LastActivityAt > now.AddMinutes(-30) &&
			s.User != null && s.User.OrganizationId == agent.OrganizationId && s.User.IsActive &&
			(!s.User.LockedUntil.HasValue || s.User.LockedUntil <= now))
			.Take(2).ToListAsync(cancellationToken);
		if (candidates.Count != 1) return await DenyBinding(agent, "AgentWindowsSessionAmbiguousOrInactive", cancellationToken);

		var old = await _db.AgentWindowsSessionBindings.IgnoreQueryFilters().Where(x => x.OrganizationId == agent.OrganizationId &&
			x.WorkstationId == agent.Id && x.WindowsSessionId == request.WindowsSessionId && x.RevokedAt == null).ToListAsync(cancellationToken);
		var existing = old.SingleOrDefault(x => x.WindowsSid == sid && x.UserId == identity.UserId &&
			x.UserSessionId == candidates[0].Id && x.WindowsUserIdentityBindingId == identity.Id &&
			x.AgentCertificateThumbprint == agent.AgentCertificateThumbprint);
		if (existing is not null)
		{
			existing.LastValidatedAt = now;
			existing.ExpiresAt = now.AddSeconds(45);
			_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId,
				UserId = identity.UserId, Action = "AgentWindowsSessionBindingRefreshed", ResourceType = "AgentWindowsSessionBinding",
				ResourceId = existing.Id, NewValues = $"Session={request.WindowsSessionId}; SID-SHA256={SidDigest(sid)}", CreatedAt = now });
			await _db.SaveChangesAsync(cancellationToken);
			return Ok(new { existing.Id, existing.UserSessionId, existing.WindowsSessionId, existing.ExpiresAt });
		}
		foreach (var prior in old) prior.RevokedAt = now;
		var binding = new AgentWindowsSessionBinding
		{
			Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId, WorkstationId = agent.Id, UserId = identity.UserId,
			UserSessionId = candidates[0].Id, WindowsUserIdentityBindingId = identity.Id, WindowsSid = sid,
			WindowsSessionId = request.WindowsSessionId, AgentCertificateThumbprint = agent.AgentCertificateThumbprint!,
			CreatedAt = now, LastValidatedAt = now, ExpiresAt = now.AddSeconds(45)
		};
		_db.AgentWindowsSessionBindings.Add(binding);
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId,
			UserId = identity.UserId, Action = "AgentWindowsSessionBindingCreated", ResourceType = "AgentWindowsSessionBinding",
			ResourceId = binding.Id, NewValues = $"Session={request.WindowsSessionId}; SID-SHA256={SidDigest(sid)}", CreatedAt = now });
		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException ex) when (IsActiveAgentWindowsSessionConflict(ex))
		{
			_logger.LogWarning("Concurrent Agent Windows-session binding was rejected by the active-session uniqueness constraint.");
			return Conflict(new { code = "AGENT_WINDOWS_SESSION_ALREADY_BOUND" });
		}
		return Ok(new { binding.Id, binding.UserSessionId, binding.WindowsSessionId, binding.ExpiresAt });
	}

	[HttpPost("windows-sessions/end")]
	public async Task<IActionResult> EndWindowsSession([FromBody] BindWindowsSessionRequest request, CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "no-store";
		var agent = GetAuthenticatedAgent();
		if (agent is null) return Unauthorized();
		var sid = request.WindowsSid?.Trim().ToUpperInvariant();
		if (sid is null || sid.Length > 184 || !WindowsSidPattern().IsMatch(sid) || request.WindowsSessionId < 0) return BadRequest();
		var now = DateTime.UtcNow;
		var rows = await _db.AgentWindowsSessionBindings.IgnoreQueryFilters().Where(x => x.OrganizationId == agent.OrganizationId &&
			x.WorkstationId == agent.Id && x.WindowsSid == sid && x.WindowsSessionId == request.WindowsSessionId &&
			x.AgentCertificateThumbprint == agent.AgentCertificateThumbprint && x.RevokedAt == null).ToListAsync(cancellationToken);
		foreach (var row in rows) row.RevokedAt = now;
		if (rows.Count > 0)
		{
			_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId, UserId = rows[0].UserId,
				Action = "AgentWindowsSessionBindingEnded", ResourceType = "AgentWindowsSessionBinding", ResourceId = rows[0].Id,
				NewValues = $"Session={request.WindowsSessionId}; SID-SHA256={SidDigest(sid)}", CreatedAt = now });
			await _db.SaveChangesAsync(cancellationToken);
		}
		return NoContent();
	}

	/// <summary>Consumes a one-time Agent launch authorization and returns only the registered executable identity.</summary>
	[HttpPost("application-sessions/validate")]
	public async Task<IActionResult> ValidateApplicationSession([FromBody] ValidateAgentApplicationSessionRequest request, CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "no-store";
		var agent = GetAuthenticatedAgent();
		if (agent is null) return Unauthorized();
		if (request.ApplicationSessionId == Guid.Empty || request.WindowsBindingId == Guid.Empty || request.WindowsSessionId < 0)
			return BadRequest("Application process context is malformed.");
		// All revocable authorization state must be observed in the same serializable
		// transaction that consumes the one-time launch permission.
		await using var authorizationTransaction = await _db.Database.BeginTransactionAsync(
			System.Data.IsolationLevel.Serializable, cancellationToken);
		var entitlement = await _licenseGuard.HasFeatureAsync(agent.OrganizationId, "kiosk", cancellationToken);
		if (!entitlement.Allowed) return await DenyBinding(agent, "AgentApplicationLaunchNotLicensed", cancellationToken, authorizationTransaction);
		var now = DateTime.UtcNow;
		var context = await _db.AgentWindowsSessionBindings.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(b =>
			b.Id == request.WindowsBindingId && b.OrganizationId == agent.OrganizationId && b.WorkstationId == agent.Id &&
			b.WindowsSessionId == request.WindowsSessionId && b.AgentCertificateThumbprint == agent.AgentCertificateThumbprint &&
			b.RevokedAt == null && b.ExpiresAt > now && b.LastValidatedAt > now.AddSeconds(-45) &&
			_db.WindowsUserIdentityBindings.IgnoreQueryFilters().Any(i => i.Id == b.WindowsUserIdentityBindingId && i.IsActive &&
				i.OrganizationId == agent.OrganizationId && i.WorkstationId == agent.Id && i.UserId == b.UserId && i.WindowsSid == b.WindowsSid), cancellationToken);
		if (context is null) return await DenyBinding(agent, "AgentApplicationBindingExpiredOrMismatch", cancellationToken, authorizationTransaction);

		var userSession = await _db.UserSessions.IgnoreQueryFilters().Include(s => s.User).AsNoTracking().SingleOrDefaultAsync(s =>
			s.Id == context.UserSessionId && s.OrganizationId == agent.OrganizationId && s.WorkstationId == agent.Id &&
			s.UserId == context.UserId && s.Status == "Active" && s.SessionEndedAt == null && s.LastActivityAt > now.AddMinutes(-30) &&
			s.User != null && s.User.OrganizationId == agent.OrganizationId && s.User.IsActive &&
			(!s.User.LockedUntil.HasValue || s.User.LockedUntil <= now), cancellationToken);
		if (userSession is null) return await DenyBinding(agent, "AgentUserSessionInactive", cancellationToken, authorizationTransaction);

		var appSession = await _db.ApplicationSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s =>
			s.Id == request.ApplicationSessionId && s.OrganizationId == agent.OrganizationId && s.UserId == context.UserId &&
			s.UserSessionId == context.UserSessionId && s.WorkstationId == agent.Id && s.ApplicationId.HasValue &&
			s.EndTime == null && s.StartTime > now.AddMinutes(-30), cancellationToken);
		if (appSession is null) return await DenyBinding(agent, "AgentApplicationSessionMismatch", cancellationToken, authorizationTransaction);

		var application = await _db.Applications.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(a =>
			a.Id == appSession.ApplicationId && a.OrganizationId == agent.OrganizationId && a.IsActive &&
			a.ApprovedInstallRoot != null && a.ApprovedExecutablePath != null && a.ExpectedExecutableSha256 != null &&
			a.ExpectedPublisherThumbprint != null && a.CredentialDeliveryMechanism == "none", cancellationToken);
		if (application is null || !WindowsApplicationIdentityPolicy.IsApproved(application.ApprovedInstallRoot,
			application.ApprovedExecutablePath, application.ExpectedExecutableSha256, application.ExpectedPublisherThumbprint,
			application.ApprovedExecutablePath, application.ExpectedExecutableSha256, application.ExpectedPublisherThumbprint))
			return await DenyBinding(agent, "AgentApplicationIdentityMismatch", cancellationToken, authorizationTransaction);

		var permission = await _db.UserPermissions.IgnoreQueryFilters().AsNoTracking().AnyAsync(p =>
			p.OrganizationId == agent.OrganizationId && p.UserId == context.UserId && p.ApplicationId == application.Id &&
			(p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Use || p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Launch) &&
			(!p.ExpiresAt.HasValue || p.ExpiresAt > now), cancellationToken);
		if (!permission) return await DenyBinding(agent, "AgentApplicationPermissionDenied", cancellationToken, authorizationTransaction);

		// An application session can authorize at most one local process start. Keep the
		// conditional consume and its audit in one transaction: a failed audit must not burn
		// the one-time authorization without recording it.
		var consumed = await _db.ApplicationSessions.IgnoreQueryFilters().Where(s => s.Id == appSession.Id &&
			s.OrganizationId == agent.OrganizationId && s.UserId == context.UserId && s.UserSessionId == context.UserSessionId &&
			s.WorkstationId == agent.Id && s.ApplicationId == application.Id && s.EndTime == null && s.LaunchAuthorizedAt == null)
			.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LaunchAuthorizedAt, now), cancellationToken);
		if (consumed != 1)
		{
			return await DenyBinding(agent, "AgentApplicationLaunchReplayDenied", cancellationToken, authorizationTransaction);
		}

		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId, UserId = context.UserId,
			Action = "AgentApplicationLaunchAuthorized", ResourceType = "ApplicationSession", ResourceId = appSession.Id, CreatedAt = now });
		await _db.SaveChangesAsync(cancellationToken);
		await authorizationTransaction.CommitAsync(cancellationToken);
		return Ok(new AgentLaunchDescriptor(appSession.Id, application.Id, application.ApprovedExecutablePath!,
			application.ExpectedPublisherThumbprint!, application.ExpectedExecutableSha256!, now.AddSeconds(15)));
	}

	private async Task<IActionResult> DenyBinding(Workstation agent, string action, CancellationToken ct,
		Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null)
	{
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = agent.OrganizationId, Action = action,
			ResourceType = "AgentWindowsSessionBinding", ResourceId = agent.Id, CreatedAt = DateTime.UtcNow });
		await _db.SaveChangesAsync(ct);
		if (transaction is not null) await transaction.CommitAsync(ct);
		_logger.LogWarning("Agent Windows session binding denied; code={FailureCode}, workstation={WorkstationId}", action, agent.Id);
		return Forbid();
	}

	private Workstation? GetAuthenticatedAgent() =>
		HttpContext.Items.TryGetValue(Nexorsys.Identity.API.Filters.AgentCertificateAuthenticationFilter.AuthenticatedAgentItemKey, out var value)
			? value as Workstation
			: null;

	private async Task AuditDenial(Workstation workstation, string action, Guid? resourceId, CancellationToken cancellationToken, string resourceType = "VaultReleaseGrant")
	{
		_db.AuditLogs.Add(new AuditLog
		{
			Id = Guid.NewGuid(), OrganizationId = workstation.OrganizationId, ResourceType = resourceType,
			ResourceId = resourceId, Action = action, CreatedAt = DateTime.UtcNow
		});
		await _db.SaveChangesAsync(cancellationToken);
	}

	private static string SidDigest(string sid) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid)))[..16];
	private static bool IsActiveAgentWindowsSessionConflict(Exception exception)
	{
		for (Exception? current = exception; current is not null; current = current.InnerException)
			if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
				postgres.ConstraintName == ActiveAgentWindowsSessionIndexName) return true;
		return false;
	}
	[GeneratedRegex("^S-1-(?:[0-9]+-){1,14}[0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex WindowsSidPattern();

	public sealed record RedeemGrantRequest(string RedemptionToken);
}

public sealed record BindWindowsSessionRequest(string WindowsSid, int WindowsSessionId);
public sealed record ValidateAgentApplicationSessionRequest(Guid ApplicationSessionId, Guid WindowsBindingId, int WindowsSessionId);
