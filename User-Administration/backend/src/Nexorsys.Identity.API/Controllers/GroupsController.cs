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
    [Route("api/v1/groups")]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IOrganizationContext _orgContext;

        public GroupsController(AppDbContext db, IOrganizationContext orgContext)
        {
            _db = db;
            _orgContext = orgContext;
        }

        [HttpGet]
        public async Task<IActionResult> ListGroups()
        {
            var groups = await _db.SecurityGroups.ToListAsync();
            return Ok(groups);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup(Guid id)
        {
            var group = await _db.SecurityGroups.FindAsync(id);
            if (group == null) return NotFound();
            return Ok(group);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req)
        {
            var group = new SecurityGroup
            {
                Id = Guid.NewGuid(),
                OrganizationId = _orgContext.OrganizationId ?? Guid.Empty,
                Name = req.Name,
                Description = req.Description,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.SecurityGroups.Add(group);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest req)
        {
            var group = await _db.SecurityGroups.FindAsync(id);
            if (group == null) return NotFound();

            group.Name = req.Name ?? group.Name;
            group.Description = req.Description ?? group.Description;
            group.IsActive = req.IsActive ?? group.IsActive;

            await _db.SaveChangesAsync();
            return Ok(group);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(Guid id)
        {
            var group = await _db.SecurityGroups.FindAsync(id);
            if (group == null) return NotFound();

            _db.SecurityGroups.Remove(group);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/members/{userId}")]
        public async Task<IActionResult> AddMember(Guid id, Guid userId)
        {
            var group = await _db.SecurityGroups.FindAsync(id);
            var user = await _db.Users.FindAsync(userId);
            if (group == null || user == null) return NotFound();

            var existing = await _db.UserGroupMemberships.FindAsync(userId, id);
            if (existing != null) return Conflict(new { error = "User is already a member" });

            _db.UserGroupMemberships.Add(new UserGroupMembership
            {
                GroupId = id,
                UserId = userId,
                OrganizationId = group.OrganizationId
            });

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var membership = await _db.UserGroupMemberships.FindAsync(userId, id);
            if (membership == null) return NotFound();

            _db.UserGroupMemberships.Remove(membership);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateGroupRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateGroupRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
