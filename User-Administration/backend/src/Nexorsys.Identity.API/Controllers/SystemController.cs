using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Infrastructure;
using System.Security.Claims;

namespace Nexorsys.Identity.API.Controllers
{
	public class SecureActionRequest
	{
		public string Password { get; set; } = string.Empty;
	}
	[ApiController]
	[Route("api/[controller]")]
	[Authorize(Policy = "SystemAdminOnly")]
	public class SystemController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly ILogger<SystemController> _logger;
		private readonly IWebHostEnvironment _env;
		private readonly IConfiguration _configuration;

		public SystemController(AppDbContext context, ILogger<SystemController> logger, IWebHostEnvironment env, IConfiguration configuration)
		{
			_context = context;
			_logger = logger;
			_env = env;
			_configuration = configuration;
		}

		private async Task<bool> VerifyAdminPassword(string password)
		{
			if (string.IsNullOrEmpty(password)) return false;

			var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId) ||
				!Guid.TryParse(User.FindFirst("org_id")?.Value, out var organizationId))
				return false;

			var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId && x.OrganizationId == organizationId);
			if (user == null || !user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow) ||
				!(string.Equals(user.Role, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) || string.Equals(user.Role, "SUPERADMIN", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrEmpty(user.PasswordHash))
				return false;

			return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
		}

		[HttpPost("backup")]
		public async Task<IActionResult> RunBackup([FromBody] SecureActionRequest request)
		{
			if (!await VerifyAdminPassword(request.Password))
				return Unauthorized(new { message = "Mot de passe administrateur incorrect." });

			try
			{
				var scriptPath = Path.Combine(_env.ContentRootPath, "..", "..", "..", "scripts", "backup-database.ps1");
				_logger.LogInformation("Starting backup with script: {Path}", scriptPath);

				var result = await RunPowerShellScript(scriptPath);

				if (result.ExitCode != 0)
				{
					_logger.LogError("Backup process failed with exitCode={ExitCode}; process output is intentionally withheld.", result.ExitCode);
					return BadRequest(new { message = "Échec de la sauvegarde. Veuillez consulter les logs serveurs.", success = false });
				}

				// Find the generated backup file
				var backupDir = Path.Combine(_env.ContentRootPath, "..", "..", "..", "backups");
				var latestBackup = new DirectoryInfo(backupDir)
					.GetFiles("db_backup_*.sql")
					.OrderByDescending(f => f.LastWriteTime)
					.FirstOrDefault();

				if (latestBackup == null)
				{
					return NotFound(new { message = "Fichier introuvable après l'exécution.", success = false });
				}

				var bytes = await System.IO.File.ReadAllBytesAsync(latestBackup.FullName);
				return File(bytes, "application/sql", latestBackup.Name);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Backup failed");
				return StatusCode(500, new { message = "Échec de la sauvegarde. Veuillez consulter les logs serveurs." });
			}
		}

		[HttpPost("restore")]
		[Consumes("multipart/form-data")]
		public async Task<IActionResult> RunRestore([FromForm] string password, IFormFile? backupFile)
		{
			if (!await VerifyAdminPassword(password))
				return Unauthorized(new { message = "Mot de passe administrateur incorrect." });

			try
			{
				string backupFilePath = "";
				if (backupFile != null && backupFile.Length > 0)
				{
					var backupDir = Path.Combine(_env.ContentRootPath, "..", "..", "..", "backups");
					if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
					backupFilePath = Path.Combine(backupDir, backupFile.FileName);
					using (var stream = new FileStream(backupFilePath, FileMode.Create))
					{
						await backupFile.CopyToAsync(stream);
					}
				}

				var scriptPath = Path.Combine(_env.ContentRootPath, "..", "..", "..", "scripts", "restore-database.ps1");
				_logger.LogInformation("Starting restore with script: {Path}", scriptPath);

				// If backupFilePath is empty, the script uses the latest backup in the directory
				string args = string.IsNullOrEmpty(backupFilePath) ? "" : $"\"{backupFilePath}\"";
				var result = await RunPowerShellScript(scriptPath, args);

				if (result.ExitCode != 0)
				{
					_logger.LogError("Restore failed. Output: {Output}", result.Output);
					return BadRequest(new { message = "Échec de la restauration. Veuillez consulter les logs serveurs.", success = false });
				}

				return Ok(new { success = true, message = "Restauration terminée avec succès." });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Restore failed");
				return StatusCode(500, new { message = "Échec de la restauration. Veuillez consulter les logs serveurs.", success = false });
			}
		}

		[HttpPost("reset")]
		public async Task<IActionResult> ResetSystem([FromBody] SecureActionRequest request)
		{
			if (!await VerifyAdminPassword(request.Password))
				return Unauthorized(new { message = "Mot de passe administrateur incorrect." });

			try
			{
				var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
				if (!Guid.TryParse(userIdString, out Guid adminUserId))
					return BadRequest(new { message = "Contexte utilisateur invalide." });

				// Erase all data EXCEPT the Super Admin user
				await _context.UserPins.ExecuteDeleteAsync();
				await _context.UserPermissions.ExecuteDeleteAsync();
				await _context.KioskSessions.ExecuteDeleteAsync();
				await _context.UserDevices.ExecuteDeleteAsync();
				await _context.UserWorkstations.ExecuteDeleteAsync();
				await _context.UserSessions.ExecuteDeleteAsync();
				await _context.WorkstationSessions.ExecuteDeleteAsync();
				await _context.SessionEvents.ExecuteDeleteAsync();
				await _context.AuditLogs.ExecuteDeleteAsync();
				await _context.AnsDelegationLogs.ExecuteDeleteAsync();
				
				// Note: depending on EF constraints, Workstations might need to be deleted after their relationships
				await _context.Workstations.ExecuteDeleteAsync();

				// Finally, delete all users except the executing super admin
				await _context.Users.Where(u => u.Id != adminUserId).ExecuteDeleteAsync();

				_logger.LogWarning("System reset executed by admin {AdminId}. All users and logs erased.", adminUserId);

				return Ok(new { success = true, message = "Système réinitialisé avec succès." });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "System reset failed");
				return StatusCode(500, new { message = "Échec de la réinitialisation. Veuillez consulter les logs serveurs.", success = false });
			}
		}

		private async Task<(int ExitCode, string Output)> RunPowerShellScript(string scriptPath, string args = "")
		{
			var processInfo = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {args}",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			var connStr = _configuration.GetConnectionString("DefaultConnection");
			var builder = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
			processInfo.EnvironmentVariables["PGPASSWORD"] = builder.Password;
			processInfo.EnvironmentVariables["NEXORSYS_DATABASE_NAME"] = builder.Database;
			processInfo.EnvironmentVariables["PGPORT"] = builder.Port.ToString();
			processInfo.EnvironmentVariables["PGUSER"] = builder.Username;
			processInfo.EnvironmentVariables["PGHOST"] = builder.Host;

			using var process = new Process { StartInfo = processInfo };
			process.Start();

			string output = await process.StandardOutput.ReadToEndAsync();
			string error = await process.StandardError.ReadToEndAsync();

			await process.WaitForExitAsync();

			if (!string.IsNullOrEmpty(error))
			{
				output += "\nERROR: " + error;
			}

			return (process.ExitCode, output);
		}
	}
}
