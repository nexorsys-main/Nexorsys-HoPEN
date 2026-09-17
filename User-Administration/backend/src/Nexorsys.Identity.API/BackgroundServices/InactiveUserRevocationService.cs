using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexorsys.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Nexorsys.Identity.API.BackgroundServices
{
	public class InactiveUserRevocationService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<InactiveUserRevocationService> _logger;
		private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
		private const int InactivityLimitDays = 21; // 3 weeks

		public InactiveUserRevocationService(IServiceProvider serviceProvider, ILogger<InactiveUserRevocationService> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Inactive User Revocation Service is starting.");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					_logger.LogInformation("Checking for inactive users at: {time}", DateTimeOffset.Now);
					await RevokeInactiveUsers();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error occurred while revoking inactive users.");
				}

				try
				{
					await Task.Delay(_checkInterval, stoppingToken);
				}
				catch (TaskCanceledException)
				{
					// Expected during shutdown
				}
			}

			_logger.LogInformation("Inactive User Revocation Service is stopping.");
		}

		private async Task RevokeInactiveUsers()
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				await RevokeInactiveUsersAsync(dbContext, _logger, DateTime.UtcNow.AddDays(-InactivityLimitDays));
			}
		}

		internal static async Task<int> RevokeInactiveUsersAsync(AppDbContext dbContext,
			ILogger<InactiveUserRevocationService> logger, DateTime cutoffDate, CancellationToken cancellationToken = default)
		{
			// This is a system job, not an HTTP request: deliberately bypass the request-tenant
			// filter, then preserve tenant ownership explicitly on every mutation and audit row.
			var inactiveUsers = await dbContext.Users.IgnoreQueryFilters()
				.Where(u => u.OrganizationId != Guid.Empty && u.IsActive &&
					((u.LastLoginAt.HasValue && u.LastLoginAt < cutoffDate) ||
					 (!u.LastLoginAt.HasValue && u.CreatedAt < cutoffDate)))
				.ToListAsync(cancellationToken);
			if (inactiveUsers.Count == 0) return 0;

			var now = DateTime.UtcNow;
			logger.LogInformation("Found {count} inactive users to revoke.", inactiveUsers.Count);
			foreach (var user in inactiveUsers)
			{
				logger.LogWarning("Revoking access for inactive user {usernameHash} due to 3 weeks of inactivity.", UsernameDigest(user.SamAccountName));
				user.IsActive = false;
				user.UpdatedAt = now;
				dbContext.AuditLogs.Add(new Core.AuditLog
				{
					Id = Guid.NewGuid(), OrganizationId = user.OrganizationId,
					Action = "Auto-Revocation (Inactivity)", ResourceType = "User", ResourceId = user.Id,
					NewValues = "IsActive: false (3 weeks inactivity limit reached)", CreatedAt = now
				});
			}

			// State transitions and their tenant-bound audit records commit together.
			await dbContext.SaveChangesAsync(cancellationToken);
			return inactiveUsers.Count;
		}

		private static string UsernameDigest(string? username)
		{
			var normalized = (username ?? string.Empty).Trim().ToUpperInvariant();
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
		}
	}
}
