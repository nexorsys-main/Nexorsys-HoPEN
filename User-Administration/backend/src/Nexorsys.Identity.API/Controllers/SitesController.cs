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
    [Route("api/v1/sites")]
    [Authorize]
    public class SitesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IOrganizationContext _orgContext;

        public SitesController(AppDbContext db, IOrganizationContext orgContext)
        {
            _db = db;
            _orgContext = orgContext;
        }

        [HttpGet]
        public async Task<IActionResult> ListSites()
        {
            var sites = await _db.Sites.ToListAsync();
            return Ok(sites);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSite(Guid id)
        {
            var site = await _db.Sites.FindAsync(id);
            if (site == null) return NotFound();
            return Ok(site);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSite([FromBody] CreateSiteRequest req)
        {
            var site = new Site
            {
                Id = Guid.NewGuid(),
                OrganizationId = _orgContext.OrganizationId ?? Guid.Empty,
                Name = req.Name,
                Code = req.Code,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.Sites.Add(site);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSite), new { id = site.Id }, site);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSite(Guid id)
        {
            var site = await _db.Sites.FindAsync(id);
            if (site == null) return NotFound();

            _db.Sites.Remove(site);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CreateSiteRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
    }
}
