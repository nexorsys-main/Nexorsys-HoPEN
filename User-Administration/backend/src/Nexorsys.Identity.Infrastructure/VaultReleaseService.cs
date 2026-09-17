using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Npgsql;

namespace Nexorsys.Identity.Infrastructure;

/// <summary>Creates secret-free short-lived grants and redeems them once on relational databases.</summary>
public sealed class VaultReleaseService : IVaultReleaseService
{
	private static readonly TimeSpan GrantLifetime = TimeSpan.FromSeconds(20);
	private readonly AppDbContext _db;
	private readonly IDataProtector _protector;

	public VaultReleaseService(AppDbContext db, IDataProtectionProvider protectionProvider)
	{
		_db = db;
		_protector = protectionProvider.CreateProtector("NexorSys.Identity.Vault.v1");
	}

	public async Task<VaultReleaseTicket?> CreateGrantAsync(AgentVaultReleaseRequest request, Guid workstationId,
		Guid organizationId, string agentCertificateThumbprint, CancellationToken cancellationToken)
	{
		if (!_db.Database.IsRelational() || !IsValidContext(workstationId, organizationId, agentCertificateThumbprint)) return null;
		var context = await LoadAuthorizedContext(request, workstationId, organizationId, agentCertificateThumbprint, cancellationToken);
		if (context is null) return null;

		var now = DateTime.UtcNow;
		var rawToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
		var grant = new VaultReleaseGrant
		{
			Id = Guid.NewGuid(), OrganizationId = organizationId, WorkstationId = workstationId,
			UserId = context.UserId, UserSessionId = request.UserSessionId, ApplicationId = request.ApplicationId,
			ApplicationSessionId = request.ApplicationSessionId, VaultEntryId = request.VaultEntryId,
			WindowsBindingId = request.WindowsBindingId, WindowsSessionId = request.WindowsSessionId,
			AgentCertificateThumbprint = WorkstationLifecycle.NormalizeThumbprint(agentCertificateThumbprint),
			TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)), CreatedAt = now, ExpiresAt = now.Add(GrantLifetime)
		};
		_db.VaultReleaseGrants.Add(grant);
		_db.AuditLogs.Add(CreateAudit(organizationId, context.UserId, "VaultReleaseGrantCreated", grant.Id, now));
		await _db.SaveChangesAsync(cancellationToken);
		return new VaultReleaseTicket(grant.Id, rawToken, grant.ExpiresAt);
	}

	public async Task<VaultReleasedCredential?> RedeemGrantAsync(string grantToken, Guid workstationId,
		Guid organizationId, string agentCertificateThumbprint, CancellationToken cancellationToken)
	{
		if (!_db.Database.IsRelational() || !IsValidContext(workstationId, organizationId, agentCertificateThumbprint) ||
			string.IsNullOrWhiteSpace(grantToken) || grantToken.Length > 128) return null;
		try
		{
			return await RedeemOnce(grantToken, workstationId, organizationId, agentCertificateThumbprint, cancellationToken);
		}
		catch (PostgresException ex) when (ex.SqlState == "40001") { return null; }
		catch (InvalidOperationException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "40001") { return null; }
	}

	private async Task<VaultReleasedCredential?> RedeemOnce(string grantToken, Guid workstationId,
		Guid organizationId, string agentCertificateThumbprint, CancellationToken cancellationToken)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(grantToken));
		await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		var now = DateTime.UtcNow;
		var grant = await _db.VaultReleaseGrants.SingleOrDefaultAsync(g =>
			g.TokenHash == hash && g.OrganizationId == organizationId && g.WorkstationId == workstationId &&
			g.AgentCertificateThumbprint == WorkstationLifecycle.NormalizeThumbprint(agentCertificateThumbprint) &&
			g.ExpiresAt > now && g.ConsumedAt == null && g.RevokedAt == null, cancellationToken);
		if (grant is null) return null;

		var context = await LoadAuthorizedContext(new AgentVaultReleaseRequest(grant.UserSessionId,
			grant.ApplicationSessionId, grant.ApplicationId, grant.VaultEntryId, grant.WindowsBindingId, grant.WindowsSessionId), workstationId, organizationId,
			agentCertificateThumbprint, cancellationToken);
		if (context is null || context.UserId != grant.UserId) return null;

		// Conditional database update is the single-use boundary. The serializable transaction
		// couples re-authorization, consumption, audit and secret read into one commit.
		var consumed = await _db.VaultReleaseGrants.Where(g => g.Id == grant.Id && g.ConsumedAt == null &&
			g.RevokedAt == null && g.ExpiresAt > now)
			.ExecuteUpdateAsync(update => update.SetProperty(g => g.ConsumedAt, now), cancellationToken);
		if (consumed != 1) return null;

		var protectedSecret = Encoding.UTF8.GetString(context.VaultEntry.EncryptedSecret);
		var secret = _protector.Unprotect(protectedSecret);
		_db.AuditLogs.Add(CreateAudit(organizationId, context.UserId, "VaultReleaseGrantConsumed", grant.Id, now));
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return new VaultReleasedCredential(grant.Id, context.Application.Id, context.VaultEntry.Username, secret);
	}

	private async Task<AuthorizedContext?> LoadAuthorizedContext(AgentVaultReleaseRequest request, Guid workstationId,
		Guid organizationId, string certificateThumbprint, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var organizationActive = await _db.Organizations.AsNoTracking().AnyAsync(o => o.Id == organizationId && o.IsActive, cancellationToken);
		if (!organizationActive) return null;

		var workstation = await _db.Workstations.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(w =>
			w.Id == workstationId && w.OrganizationId == organizationId && w.IsActive &&
			w.EnrollmentState == WorkstationLifecycle.Active &&
			w.AgentCertificateValidatedAt != null && w.AgentCertificateValidatedAt <= now &&
			w.AgentCertificateThumbprint == WorkstationLifecycle.NormalizeThumbprint(certificateThumbprint), cancellationToken);
		if (workstation is null) return null;

		var userSession = await _db.UserSessions.IgnoreQueryFilters().Include(s => s.User).AsNoTracking().SingleOrDefaultAsync(s =>
			s.Id == request.UserSessionId && s.OrganizationId == organizationId && s.WorkstationId == workstationId &&
			s.Status == "Active" && s.SessionEndedAt == null && s.LastActivityAt > now.AddMinutes(-30), cancellationToken);
		if (userSession?.User is null || userSession.User.OrganizationId != organizationId || !userSession.User.IsActive ||
			(userSession.User.LockedUntil.HasValue && userSession.User.LockedUntil > now)) return null;
		if (!request.WindowsBindingId.HasValue || !request.WindowsSessionId.HasValue || request.WindowsSessionId < 0) return null;
		var windowsBinding = await _db.AgentWindowsSessionBindings.IgnoreQueryFilters().AsNoTracking().AnyAsync(b =>
			b.Id == request.WindowsBindingId && b.OrganizationId == organizationId && b.WorkstationId == workstationId &&
			b.UserId == userSession.UserId && b.UserSessionId == userSession.Id && b.WindowsSessionId == request.WindowsSessionId &&
			b.AgentCertificateThumbprint == WorkstationLifecycle.NormalizeThumbprint(certificateThumbprint) &&
			b.RevokedAt == null && b.ExpiresAt > now && b.LastValidatedAt > now.AddSeconds(-45) &&
			_db.WindowsUserIdentityBindings.IgnoreQueryFilters().Any(i => i.Id == b.WindowsUserIdentityBindingId &&
				i.OrganizationId == organizationId && i.WorkstationId == workstationId && i.UserId == userSession.UserId &&
				i.WindowsSid == b.WindowsSid && i.IsActive), cancellationToken);
		if (!windowsBinding) return null;

		var application = await _db.Applications.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(a =>
			a.Id == request.ApplicationId && a.OrganizationId == organizationId && a.IsActive &&
			a.ApprovedInstallRoot != null && a.ExpectedPublisherThumbprint != null && a.CredentialDeliveryMechanism == "none", cancellationToken);
		if (application is null) return null;
		var permission = await _db.UserPermissions.IgnoreQueryFilters().AsNoTracking().AnyAsync(p => p.OrganizationId == organizationId &&
			p.UserId == userSession.UserId && p.ApplicationId == application.Id &&
			(p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Use || p.PermissionLevel.ToLower() == ApplicationPermissionPolicy.Launch) &&
			(!p.ExpiresAt.HasValue || p.ExpiresAt > now), cancellationToken);
		if (!permission) return null;

		var applicationSession = await _db.ApplicationSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s =>
			s.Id == request.ApplicationSessionId && s.OrganizationId == organizationId &&
			s.UserId == userSession.UserId && s.UserSessionId == userSession.Id && s.WorkstationId == workstationId && s.EndTime == null &&
			s.ApplicationId == application.Id && s.StartTime > now.AddMinutes(-30), cancellationToken);
		if (applicationSession is null) return null;

		var vaultEntry = await _db.VaultEntries.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(v =>
			v.Id == request.VaultEntryId && v.OrganizationId == organizationId && v.ApplicationId == application.Id &&
			v.Status == "active" && (!v.UserId.HasValue || v.UserId == userSession.UserId) &&
			(!v.ExpiresAt.HasValue || v.ExpiresAt > now), cancellationToken);
		return vaultEntry is null ? null : new AuthorizedContext(userSession.UserId, application, vaultEntry);
	}

	private static AuditLog CreateAudit(Guid organizationId, Guid userId, string action, Guid resourceId, DateTime now) => new()
	{
		Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = userId,
		Action = action, ResourceType = "VaultReleaseGrant", ResourceId = resourceId, CreatedAt = now
	};

	private static bool IsValidContext(Guid workstationId, Guid organizationId, string thumbprint) =>
		workstationId != Guid.Empty && organizationId != Guid.Empty && !string.IsNullOrWhiteSpace(thumbprint);

	private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	private sealed record AuthorizedContext(Guid UserId, Application Application, VaultEntry VaultEntry);
}
