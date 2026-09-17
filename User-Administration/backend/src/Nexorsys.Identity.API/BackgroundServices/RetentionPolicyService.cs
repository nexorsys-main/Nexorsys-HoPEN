using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.BackgroundServices
{
	public class RetentionPolicyService : BackgroundService
	{
		private readonly ILogger<RetentionPolicyService> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

		public RetentionPolicyService(ILogger<RetentionPolicyService> logger, IServiceProvider serviceProvider)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("RetentionPolicyService is starting.");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await EnforceRetentionPoliciesAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error occurred executing retention policies.");
				}

				await Task.Delay(_checkInterval, stoppingToken);
			}
		}

		private async Task EnforceRetentionPoliciesAsync(CancellationToken stoppingToken)
		{
			using var scope = _serviceProvider.CreateScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			var authEventsCutoff = DateTime.UtcNow.AddYears(-3);
			var sessionsCutoff = DateTime.UtcNow.AddYears(-1);
			await EnforceRetentionPoliciesAsync(dbContext, _logger, authEventsCutoff, sessionsCutoff, stoppingToken);
		}

		internal static async Task<(int DeletedEvents, int DeletedSessions)> EnforceRetentionPoliciesAsync(
			AppDbContext dbContext, ILogger<RetentionPolicyService> logger, DateTime authEventsCutoff,
			DateTime sessionsCutoff, CancellationToken stoppingToken = default)
		{
			// System retention runs outside HTTP and therefore has no request organization.
			// Cross-tenant deletion is intentional, while the age predicate is always applied.
			logger.LogInformation("Purging AuthenticationEvents older than {Cutoff}", authEventsCutoff);
			var deletedEvents = await dbContext.AuthenticationEvents.IgnoreQueryFilters()
				.Where(e => e.Timestamp < authEventsCutoff)
				.ExecuteDeleteAsync(stoppingToken);

			logger.LogInformation("Purged {Count} old AuthenticationEvents.", deletedEvents);

			logger.LogInformation("Purging UserSessions older than {Cutoff}", sessionsCutoff);
			var deletedSessions = await dbContext.UserSessions.IgnoreQueryFilters()
				.Where(s => s.SessionStartedAt < sessionsCutoff)
				.ExecuteDeleteAsync(stoppingToken);

			logger.LogInformation("Purged {Count} old UserSessions.", deletedSessions);
			return (deletedEvents, deletedSessions);
		}
	}
}
