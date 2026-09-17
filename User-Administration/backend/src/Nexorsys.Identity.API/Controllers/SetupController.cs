using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController(AppDbContext dbContext, IAuditService auditService, ILogger<SetupController> logger) : ControllerBase
{
    public class SetupRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    [HttpPost("initialize")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> InitializeAdmin([FromBody] SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        // Check if ANY local profile has a password set. If yes, system is already initialized.
        bool isInitialized = await dbContext.Users.AnyAsync(u => u.IsLocalProfile && !string.IsNullOrEmpty(u.PasswordHash));
        
        if (isInitialized)
        {
            logger.LogWarning("Attempt to initialize admin when system is already initialized.");
            return Forbid();
        }

        var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.SamAccountName == request.Username && u.IsLocalProfile);
        
        if (adminUser == null)
            return NotFound("Administrator profile not found.");

        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        adminUser.UpdatedAt = DateTime.UtcNow;

        await auditService.LogAsync("System Initialization: Admin Password Set", "System", adminUser.Id, newValues: "PasswordHash=***");
        await dbContext.SaveChangesAsync();

        logger.LogInformation("System initialized successfully for user {Username}", request.Username);
        return Ok(new { message = "Administrator password initialized successfully." });
    }
}
