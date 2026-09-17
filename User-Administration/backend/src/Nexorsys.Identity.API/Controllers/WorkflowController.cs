using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using Nexorsys.Identity.API.Hubs;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class WorkflowController : ControllerBase
	{
		private readonly ILogger<WorkflowController> _logger;
		private readonly AppDbContext _context;
		private readonly IHubContext<IdentityHub> _hubContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public WorkflowController(
			ILogger<WorkflowController> logger,
			AppDbContext context,
			IHubContext<IdentityHub> hubContext,
			IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_logger = logger;
			_context = context;
			_hubContext = hubContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetAll()
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var workflows = _context.Workflows.Where(w => w.OrganizationId == organizationId &&
				_context.Users.Any(u => u.Id == w.UserId && u.OrganizationId == organizationId))
				.OrderByDescending(w => w.CreatedAt)
				.Select(w => new { w.Id, w.OrganizationId, w.Type, w.UserId, w.Status, w.CreatedAt, w.UpdatedAt })
				.ToList();
			return Ok(workflows);
		}

		[HttpGet("{id}")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> GetById(Guid id)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var organizationId = _organizationContext.OrganizationId.Value;
			var workflow = _context.Workflows.Where(w => w.OrganizationId == organizationId &&
				_context.Users.Any(u => u.Id == w.UserId && u.OrganizationId == organizationId))
				.FirstOrDefault(w => w.Id == id);
			if (workflow == null) return NotFound();
			return Ok(new { workflow.Id, workflow.OrganizationId, workflow.Type, workflow.UserId, workflow.Status, workflow.CreatedAt, workflow.UpdatedAt });
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] WorkflowCreateRequest request)
		{
			var entitlementFailure = await RequireIdentityEntitlementAsync();
			if (entitlementFailure is not null) return entitlementFailure;
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			if (request == null || string.IsNullOrWhiteSpace(request.Type)) return BadRequest();
			var actorClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
			if (!Guid.TryParse(actorClaim, out var actorId)) return Unauthorized();
			if (actorId != request.UserId && !User.IsInRole("ADMIN") && !User.IsInRole("Admin") && !User.IsInRole("ADMIN_RH") && !User.IsInRole("ADMIN_DSI") && !User.IsInRole("SUPERADMIN")) return Forbid();
			if (request.Type.Length > 100 || (request.Comments?.Length ?? 0) > 2000 || (request.FormData?.Length ?? 0) > 16_384) return BadRequest();
			if (!await _context.Users.AnyAsync(u => u.Id == request.UserId && u.OrganizationId == _organizationContext.OrganizationId.Value)) return BadRequest("Invalid user.");
			var workflow = new Workflow { Id = Guid.NewGuid(), OrganizationId = _organizationContext.OrganizationId.Value, Type = request.Type.Trim(), UserId = request.UserId, Comments = request.Comments, FormData = request.FormData };
			workflow.CreatedAt = DateTime.UtcNow;
			workflow.UpdatedAt = DateTime.UtcNow;
			workflow.Status = "pending";

			_context.Workflows.Add(workflow);
			Nexorsys.Identity.API.Services.TenantActorOwnership.Ensure(_context, actorId, workflow.OrganizationId);
			_context.AuditLogs.Add(new AuditLog
			{
				Id = Guid.NewGuid(), OrganizationId = workflow.OrganizationId, UserId = actorId,
				Action = "Création de flux de travail", ResourceType = "Workflow", ResourceId = workflow.Id,
				NewValues = AuditRedactor.Redact($"Type={workflow.Type}; Status={workflow.Status}"),
				IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
				UserAgent = "api", CreatedAt = DateTime.UtcNow
			});
			await _context.SaveChangesAsync();

			try
			{
				await _hubContext.Clients.Group(IdentityHub.AdminGroupName(workflow.OrganizationId)).SendAsync("OnWorkflowChanged", new
				{
					id = workflow.Id,
					type = workflow.Type,
					userId = workflow.UserId,
					status = workflow.Status
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SignalR notification failed for workflow creation");
			}

			return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, new { workflow.Id, workflow.Type, workflow.UserId, workflow.Status, workflow.CreatedAt });
		}

		[HttpPatch("{id}/status")]
		[Authorize(Policy = "RhOrAdmin")]
		public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status)
		{
			try
			{
				var entitlementFailure = await RequireIdentityEntitlementAsync();
				if (entitlementFailure is not null) return entitlementFailure;
				if (!_organizationContext.OrganizationId.HasValue) return Forbid();
				if (status is not ("approved" or "rejected")) return BadRequest("Unsupported workflow state.");
				var organizationId = _organizationContext.OrganizationId.Value;
				var workflow = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == id && w.OrganizationId == organizationId &&
					_context.Users.Any(u => u.Id == w.UserId && u.OrganizationId == organizationId));
				if (workflow == null) return NotFound();
				if (!string.Equals(workflow.Status, "pending", StringComparison.Ordinal))
					return Conflict(new { code = "WORKFLOW_ALREADY_DECIDED" });

				_logger.LogInformation("Updating workflow {Id} status to {Status}", id, status);
				string oldStatus = workflow.Status;
				var now = DateTime.UtcNow;
				await using var transaction = _context.Database.IsRelational()
					? await _context.Database.BeginTransactionAsync()
					: null;
				if (transaction is not null)
				{
					var transitioned = await _context.Workflows
						.Where(w => w.Id == id && w.OrganizationId == organizationId && w.Status == "pending" &&
							_context.Users.Any(u => u.Id == w.UserId && u.OrganizationId == organizationId))
						.ExecuteUpdateAsync(setters => setters
							.SetProperty(w => w.Status, status)
							.SetProperty(w => w.UpdatedAt, now));
					if (transitioned != 1) return Conflict(new { code = "WORKFLOW_ALREADY_DECIDED" });
					_context.Entry(workflow).State = EntityState.Detached;
					// Keep the detached instance accurate for the response and SignalR payload.
					workflow.Status = status;
					workflow.UpdatedAt = now;
				}
				else
				{
					workflow.Status = status;
					workflow.UpdatedAt = now;
				}

				var actorId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsedActorId)
					? parsedActorId : (Guid?)null;
				Nexorsys.Identity.API.Services.TenantActorOwnership.Ensure(_context, actorId, workflow.OrganizationId);
				_context.AuditLogs.Add(new AuditLog
				{
					Id = Guid.NewGuid(), OrganizationId = workflow.OrganizationId,
					UserId = actorId,
					Action = "Mise à jour du statut du workflow", ResourceType = "Workflow", ResourceId = workflow.Id,
					OldValues = AuditRedactor.Redact($"Status={oldStatus}"), NewValues = AuditRedactor.Redact($"Status={status}"),
					IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
					UserAgent = "api", CreatedAt = DateTime.UtcNow
				});
				await _context.SaveChangesAsync();
				if (transaction is not null) await transaction.CommitAsync();

				try
				{
					await _hubContext.Clients.Group(IdentityHub.AdminGroupName(workflow.OrganizationId)).SendAsync("OnWorkflowChanged", new
					{
						id = workflow.Id,
						status = workflow.Status
					});
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "SignalR notification failed for workflow status update");
				}

				return Ok(new
				{
					workflow.Id,
					workflow.OrganizationId,
					workflow.Type,
					workflow.UserId,
					workflow.Status,
					workflow.CreatedAt,
					workflow.UpdatedAt
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating workflow {Id} status", id);
				return StatusCode(500, "Workflow update failed.");
			}
		}

		private async Task<IActionResult?> RequireIdentityEntitlementAsync()
		{
			if (!_organizationContext.OrganizationId.HasValue) return Forbid();
			var entitlement = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity");
			return entitlement.Allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { code = entitlement.Code });
		}
	}

	public sealed class WorkflowCreateRequest
	{
		public string Type { get; set; } = string.Empty;
		public Guid UserId { get; set; }
		public string? Comments { get; set; }
		public string? FormData { get; set; }
	}
}
