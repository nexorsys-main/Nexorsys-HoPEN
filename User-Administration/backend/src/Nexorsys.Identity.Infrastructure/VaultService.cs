using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure;

public sealed class VaultService : IVaultService
{
	private readonly AppDbContext _db;
	private readonly IDataProtector _protector;

	public VaultService(AppDbContext db, IDataProtectionProvider protectionProvider)
	{
		_db = db;
		_protector = protectionProvider.CreateProtector("NexorSys.Identity.Vault.v1");
	}

	public async Task<VaultEntry> StoreAsync(Guid organizationId, Guid applicationId, Guid? siteId, string name, string credentialType, Guid? userId, string username, string secret, Guid actorId, CancellationToken cancellationToken = default)
	{
		if (organizationId == Guid.Empty || applicationId == Guid.Empty || actorId == Guid.Empty)
			throw new ArgumentException("Vault ownership and actor identifiers are required.");
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Vault name, username and secret are required.");
		if (!await _db.Applications.IgnoreQueryFilters().AnyAsync(a => a.Id == applicationId &&
			a.OrganizationId == organizationId && a.IsActive, cancellationToken))
			throw new InvalidOperationException("The Vault application must belong to the target organization and be active.");
		if (userId.HasValue && !await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId.Value &&
			u.OrganizationId == organizationId, cancellationToken))
			throw new InvalidOperationException("The Vault owner must belong to the target organization.");
		if (!await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == actorId &&
			u.OrganizationId == organizationId && u.IsActive, cancellationToken))
			throw new InvalidOperationException("The Vault actor must be an active user in the target organization.");

		var entry = new VaultEntry
		{
			Id = Guid.NewGuid(),
			OrganizationId = organizationId,
			ApplicationId = applicationId,
			SiteId = siteId,
			Name = name,
			CredentialType = credentialType,
			UserId = userId,
			Username = username,
			EncryptedSecret = _protector.Protect(Encoding.UTF8.GetBytes(secret)),
			CreatedBy = actorId,
			UpdatedBy = actorId,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			Status = "ACTIVE",
			SecretVersion = 1
		};
		_db.VaultEntries.Add(entry);
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId, Action = "VaultCredentialCreated", ResourceType = "VaultEntry", ResourceId = entry.Id, NewValues = $"Name={name}", CreatedAt = DateTime.UtcNow });
		await _db.SaveChangesAsync(cancellationToken);
		return entry;
	}

	public async Task<bool> CanUseAsync(Guid organizationId, Guid vaultEntryId, Guid userId, Guid workstationId, Guid sessionId, CancellationToken cancellationToken = default)
	{
		if (organizationId == Guid.Empty || userId == Guid.Empty || workstationId == Guid.Empty || sessionId == Guid.Empty)
			return false;
		var entry = await _db.VaultEntries.AsNoTracking().SingleOrDefaultAsync(x =>
			x.Id == vaultEntryId && x.OrganizationId == organizationId && x.Status == "ACTIVE" && x.RevokedAt == null &&
			(!x.UserId.HasValue || x.UserId == userId) && (!x.ExpiresAt.HasValue || x.ExpiresAt > DateTime.UtcNow), cancellationToken);
		if (entry is null) return false;
		var application = await _db.Applications.AsNoTracking().SingleOrDefaultAsync(a =>
			a.Id == entry.ApplicationId && a.OrganizationId == organizationId && a.IsActive, cancellationToken);
		if (application is null) return false;
		if (!await _db.UserPermissions.AnyAsync(p => p.OrganizationId == organizationId && p.UserId == userId &&
			p.ApplicationId == entry.ApplicationId && (!p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow), cancellationToken))
			return false;

		var session = await _db.UserSessions.Include(x => x.User).Include(x => x.Workstation).AsNoTracking().SingleOrDefaultAsync(x =>
			x.Id == sessionId && x.OrganizationId == organizationId && x.UserId == userId &&
			x.WorkstationId == workstationId && x.Status == "Active" && x.SessionEndedAt == null &&
			x.LastActivityAt > DateTime.UtcNow.AddMinutes(-30), cancellationToken);
		if (session?.User is null || session.User.OrganizationId != organizationId || !session.User.IsActive ||
			(session.User.LockedUntil.HasValue && session.User.LockedUntil > DateTime.UtcNow) ||
			 session.Workstation is null || session.Workstation.OrganizationId != organizationId || !session.Workstation.IsActive ||
			 session.Workstation.EnrollmentState != WorkstationLifecycle.Active)
			return false;

		return await _db.ApplicationSessions.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId &&
			x.UserSessionId == sessionId && x.WorkstationId == workstationId && x.ApplicationId == application.Id &&
			x.EndTime == null, cancellationToken);
	}

	public async Task<IEnumerable<VaultEntry>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
	{
		return await _db.VaultEntries
			.AsNoTracking()
			.Where(x => x.OrganizationId == organizationId)
			.Select(x => new VaultEntry { Id = x.Id, OrganizationId = x.OrganizationId, ApplicationId = x.ApplicationId, SiteId = x.SiteId, Name = x.Name, CredentialType = x.CredentialType, UserId = x.UserId, Username = x.Username, Status = x.Status, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, LastRotatedAt = x.LastRotatedAt, NextRotationAt = x.NextRotationAt, RevokedAt = x.RevokedAt, LastUsedAt = x.LastUsedAt, ExpiresAt = x.ExpiresAt, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy, SecretVersion = x.SecretVersion, Version = x.Version })
			.ToListAsync(cancellationToken);
	}

	public async Task<VaultEntry?> GetAsync(Guid organizationId, Guid vaultEntryId, CancellationToken cancellationToken = default)
	{
		return await _db.VaultEntries
			.AsNoTracking()
			.Where(x => x.Id == vaultEntryId && x.OrganizationId == organizationId)
			.Select(x => new VaultEntry { Id = x.Id, OrganizationId = x.OrganizationId, ApplicationId = x.ApplicationId, SiteId = x.SiteId, Name = x.Name, CredentialType = x.CredentialType, UserId = x.UserId, Username = x.Username, Status = x.Status, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, LastRotatedAt = x.LastRotatedAt, NextRotationAt = x.NextRotationAt, RevokedAt = x.RevokedAt, LastUsedAt = x.LastUsedAt, ExpiresAt = x.ExpiresAt, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy, SecretVersion = x.SecretVersion, Version = x.Version })
			.SingleOrDefaultAsync(cancellationToken);
	}

	public async Task<VaultEntry> RotateAsync(Guid organizationId, Guid vaultEntryId, string newSecret, Guid actorId, CancellationToken cancellationToken = default)
	{
		var entry = await _db.VaultEntries.SingleOrDefaultAsync(x => x.Id == vaultEntryId && x.OrganizationId == organizationId, cancellationToken);
		if (entry == null) throw new InvalidOperationException("Vault credential not found.");
		if (entry.Status == "REVOKED") throw new InvalidOperationException("Cannot rotate a revoked credential.");
		
		entry.EncryptedSecret = _protector.Protect(Encoding.UTF8.GetBytes(newSecret));
		entry.LastRotatedAt = DateTime.UtcNow;
		entry.UpdatedAt = DateTime.UtcNow;
		entry.UpdatedBy = actorId;
		entry.SecretVersion++;
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId, Action = "VaultCredentialRotated", ResourceType = "VaultEntry", ResourceId = entry.Id, NewValues = $"Name={entry.Name}", CreatedAt = DateTime.UtcNow });
		
		// Revoke active release grants
		var activeGrants = await _db.VaultReleaseGrants.Where(x => x.VaultEntryId == vaultEntryId && x.OrganizationId == organizationId && x.ExpiresAt > DateTime.UtcNow && x.ConsumedAt == null).ToListAsync(cancellationToken);
		foreach (var grant in activeGrants)
		{
			grant.ExpiresAt = DateTime.UtcNow; // effectively revoke
		}
		
		await _db.SaveChangesAsync(cancellationToken);
		return entry;
	}

	public async Task<VaultEntry> RevokeAsync(Guid organizationId, Guid vaultEntryId, Guid actorId, CancellationToken cancellationToken = default)
	{
		var entry = await _db.VaultEntries.SingleOrDefaultAsync(x => x.Id == vaultEntryId && x.OrganizationId == organizationId, cancellationToken);
		if (entry == null) throw new InvalidOperationException("Vault credential not found.");
		if (entry.Status == "REVOKED") return entry;

		entry.Status = "REVOKED";
		entry.RevokedAt = DateTime.UtcNow;
		entry.UpdatedAt = DateTime.UtcNow;
		entry.UpdatedBy = actorId;
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId, Action = "VaultCredentialRevoked", ResourceType = "VaultEntry", ResourceId = entry.Id, NewValues = $"Name={entry.Name}", CreatedAt = DateTime.UtcNow });
		
		// Revoke active release grants
		var activeGrants = await _db.VaultReleaseGrants.Where(x => x.VaultEntryId == vaultEntryId && x.OrganizationId == organizationId && x.ExpiresAt > DateTime.UtcNow && x.ConsumedAt == null).ToListAsync(cancellationToken);
		foreach (var grant in activeGrants)
		{
			grant.ExpiresAt = DateTime.UtcNow;
		}

		await _db.SaveChangesAsync(cancellationToken);
		return entry;
	}

	public async Task DeleteAsync(Guid organizationId, Guid vaultEntryId, Guid actorId, CancellationToken cancellationToken = default)
	{
		var entry = await _db.VaultEntries.SingleOrDefaultAsync(x => x.Id == vaultEntryId && x.OrganizationId == organizationId, cancellationToken);
		if (entry == null) return;
		
		_db.VaultEntries.Remove(entry);
		_db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = actorId, Action = "VaultCredentialDeleted", ResourceType = "VaultEntry", ResourceId = entry.Id, NewValues = $"Name={entry.Name}", CreatedAt = DateTime.UtcNow });
		await _db.SaveChangesAsync(cancellationToken);
	}
}
