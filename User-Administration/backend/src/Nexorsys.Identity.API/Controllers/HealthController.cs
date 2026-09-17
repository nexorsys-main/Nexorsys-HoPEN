using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.API.Services;
using System.Text.Json;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class HealthController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly ILogger<HealthController> _logger;
		private readonly IHostEnvironment? _environment;
		private readonly SignedLicenseRevocationSnapshotStore? _revocationSnapshots;

		public HealthController(AppDbContext dbContext, ILogger<HealthController> logger,
			IHostEnvironment? environment = null, SignedLicenseRevocationSnapshotStore? revocationSnapshots = null)
		{
			_dbContext = dbContext;
			_logger = logger;
			_environment = environment;
			_revocationSnapshots = revocationSnapshots;
		}

		[HttpGet("status")]
		public async Task<IActionResult> GetStatus()
		{
			var dbStatus = "Disconnected";
			try
			{
				if (!await _dbContext.Database.CanConnectAsync()) throw new InvalidOperationException("Database connection unavailable.");
				if ((await _dbContext.Database.GetPendingMigrationsAsync()).Any())
					return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Status = "NotReady", Database = "SchemaOutOfDate" });
				dbStatus = "Ready";
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Readiness check failed for database.");
				return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Status = "NotReady", Database = "Unavailable" });
			}

			if (_environment?.IsProduction() == true)
			{
				var revocationStatus = _revocationSnapshots?.VerifyCurrentSnapshot();
				if (revocationStatus is null || !revocationStatus.IsValid)
				{
					_logger.LogError("Readiness check failed because the vendor-signed license revocation snapshot is unavailable or invalid; code={FailureCode}.",
						revocationStatus?.Code ?? "REVOCATION_SNAPSHOT_STORE_UNAVAILABLE");
					return StatusCode(StatusCodes.Status503ServiceUnavailable,
						new { Status = "NotReady", Licensing = "RevocationSnapshotUnavailable" });
				}
			}

			return Ok(new
			{
				Status = "Ready",
				Database = dbStatus,
				Timestamp = DateTime.UtcNow
			});
		}
	}
}
