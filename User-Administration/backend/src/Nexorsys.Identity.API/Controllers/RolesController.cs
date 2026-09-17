using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace Nexorsys.Identity.API.Controllers
{
    [ApiController]
    [Route("api/v1/roles")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IOrganizationContext _orgContext;

        public RolesController(AppDbContext db, IOrganizationContext orgContext)
        {
            _db = db;
            _orgContext = orgContext;
        }

        [HttpGet]
        public async Task<IActionResult> ListRoles()
        {
            var roles = await _db.RoleDefinitions.ToListAsync();
            return Ok(roles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(Guid id)
        {
            var role = await _db.RoleDefinitions.FindAsync(id);
            if (role == null) return NotFound();
            return Ok(role);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req)
        {
            var role = new RoleDefinition
            {
                Id = Guid.NewGuid(),
                OrganizationId = _orgContext.OrganizationId ?? Guid.Empty,
                Name = req.Name,
                Description = req.Description,
                IsSystemRole = false,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.RoleDefinitions.Add(role);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(Guid id)
        {
            var role = await _db.RoleDefinitions.FindAsync(id);
            if (role == null) return NotFound();
            if (role.IsSystemRole) return Forbid(); // Cannot delete system roles

            _db.RoleDefinitions.Remove(role);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/permissions/{permissionId}")]
        public async Task<IActionResult> AddPermission(Guid id, Guid permissionId)
        {
            var role = await _db.RoleDefinitions.FindAsync(id);
            var perm = await _db.PermissionDefinitions.FindAsync(permissionId);
            if (role == null || perm == null) return NotFound();

            var existing = await _db.RolePermissions.FindAsync(id, permissionId);
            if (existing != null) return Conflict(new { error = "Role already has this permission" });

            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = id,
                PermissionId = permissionId,
                OrganizationId = role.OrganizationId
            });

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}/permissions/{permissionId}")]
        public async Task<IActionResult> RemovePermission(Guid id, Guid permissionId)
        {
            var membership = await _db.RolePermissions.FindAsync(id, permissionId);
            if (membership == null) return NotFound();

            _db.RolePermissions.Remove(membership);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateRoleRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
