using System.Text;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services;

public interface IGlobalSettingsOperationProcessor
{
	Task CommitAsync(string settingsJson, IAuditService audit, CancellationToken cancellationToken = default);
	Task RecoverPendingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists an update intent and its audit in PostgreSQL before atomically writing the
/// host settings file. Pending operations are replayed before the API starts serving.
/// </summary>
public sealed class GlobalSettingsOperationProcessor : IGlobalSettingsOperationProcessor
{
	private readonly AppDbContext _db;
	private readonly string _settingsFilePath;
	private readonly IOrganizationContext _organization;

	public GlobalSettingsOperationProcessor(AppDbContext db, IWebHostEnvironment environment, IOrganizationContext organization)
	{
		_db = db;
		_settingsFilePath = Path.Combine(environment.ContentRootPath, "custom_settings.json");
		_organization = organization;
	}

	public async Task CommitAsync(string settingsJson, IAuditService audit, CancellationToken cancellationToken = default)
	{
		await using var operationLock = await AcquireLockAsync(cancellationToken);
		await ApplyPendingUnderLockAsync(cancellationToken);

		var organizationId = _organization.OrganizationId ?? Guid.Empty;
		if (organizationId == Guid.Empty)
			throw new InvalidOperationException("An authenticated organization is required for a settings operation audit.");

		var operation = new GlobalSettingsOperation
		{
			Id = Guid.NewGuid(),
			OrganizationId = organizationId,
			SettingsJson = settingsJson,
			Status = "Pending",
			CreatedAt = DateTime.UtcNow
		};
		_db.GlobalSettingsOperations.Add(operation);
		audit.Add("Global settings update pending", "SystemSettings", operation.Id,
			newValues: "Durable settings update intent recorded; file application pending.");
		await _db.SaveChangesAsync(cancellationToken);
		await ApplyPendingUnderLockAsync(cancellationToken);
	}

	public async Task RecoverPendingAsync(CancellationToken cancellationToken = default)
	{
		await using var operationLock = await AcquireLockAsync(cancellationToken);
		await ApplyPendingUnderLockAsync(cancellationToken);
	}

	private async Task ApplyPendingUnderLockAsync(CancellationToken cancellationToken)
	{
		var pending = await _db.GlobalSettingsOperations
			.Where(operation => operation.Status == "Pending")
			.OrderBy(operation => operation.Sequence)
			.ToListAsync(cancellationToken);
		foreach (var operation in pending)
		{
			await WriteSettingsAtomicallyAsync(operation.SettingsJson, cancellationToken);
			operation.Status = "Completed";
			operation.CompletedAt = DateTime.UtcNow;
			operation.SettingsJson = string.Empty;
			var audit = await _db.AuditLogs.IgnoreQueryFilters().SingleOrDefaultAsync(entry =>
				entry.OrganizationId == operation.OrganizationId && entry.ResourceId == operation.Id &&
				entry.ResourceType == "SystemSettings" && entry.Action == "Global settings update pending", cancellationToken);
			if (audit is null)
				throw new InvalidOperationException("The durable settings operation has no matching pending audit entry.");
			audit.Action = "Update Global Settings";
			audit.NewValues = "Global configuration updated by administrator";
			await _db.SaveChangesAsync(cancellationToken);
		}
	}

	private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
	{
		var directory = Path.GetDirectoryName(_settingsFilePath)!;
		Directory.CreateDirectory(directory);
		var lockPath = Path.Combine(directory, $".{Path.GetFileName(_settingsFilePath)}.operation.lock");
		for (var attempt = 0; ; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1,
					FileOptions.WriteThrough);
			}
			catch (IOException) when (attempt < 300)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
			}
		}
	}

	private async Task WriteSettingsAtomicallyAsync(string json, CancellationToken cancellationToken)
	{
		var directory = Path.GetDirectoryName(_settingsFilePath)!;
		Directory.CreateDirectory(directory);
		var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_settingsFilePath)}.{Guid.NewGuid():N}.tmp");
		try
		{
			await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				var bytes = Encoding.UTF8.GetBytes(json);
				await stream.WriteAsync(bytes, cancellationToken);
				stream.Flush(flushToDisk: true);
			}
			File.Move(temporaryPath, _settingsFilePath, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}
}

public sealed class GlobalSettingsOperationRecoveryService(IServiceScopeFactory scopeFactory, ILogger<GlobalSettingsOperationRecoveryService> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var scope = scopeFactory.CreateScope();
		var processor = scope.ServiceProvider.GetRequiredService<IGlobalSettingsOperationProcessor>();
		try
		{
			await processor.RecoverPendingAsync(cancellationToken);
		}
		catch (Exception exception)
		{
			logger.LogCritical(exception, "Could not recover pending global settings operations; API startup is aborted to avoid serving inconsistent settings.");
			throw;
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
