using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using System.Security.Claims;

namespace Nexorsys.Identity.Infrastructure
{
	public class AuditService : IAuditService
	{
		private readonly AppDbContext _context;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IOrganizationContext _organizationContext;

		public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor, IOrganizationContext organizationContext)
		{
			_context = context;
			_httpContextAccessor = httpContextAccessor;
			_organizationContext = organizationContext;
		}

		public async Task LogAsync(string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null, Guid? userId = null)
		{
			Add(action, resourceType, resourceId, oldValues, newValues, userId);
			await _context.SaveChangesAsync();
		}

		public void Add(string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null, Guid? userId = null)
		{
			if (!_organizationContext.OrganizationId.HasValue || _organizationContext.OrganizationId.Value == Guid.Empty)
				throw new InvalidOperationException("Audit organization context is unavailable.");
			var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

			if (ip == "::1")
			{
				ip = "127.0.0.1";
			}

			// Only use server-authenticated device identity as the machine marker.
			var machineName = _httpContextAccessor.HttpContext?.Items["AuthenticatedWorkstationId"]?.ToString();
			if (string.IsNullOrEmpty(machineName))
			{
				machineName = "unbound-client";
			}

			var auditLog = new AuditLog
			{
				Id = Guid.NewGuid(),
				OrganizationId = _organizationContext.OrganizationId.Value,
				Action = action,
				ResourceType = resourceType,
				ResourceId = resourceId,
				OldValues = AuditRedactor.Redact(oldValues),
				NewValues = AuditRedactor.Redact(newValues),
				CreatedAt = DateTime.UtcNow,
				IpAddress = ip,
				UserAgent = machineName
			};

			if (userId.HasValue)
			{
				EnsureAuditUserBelongsToOrganization(userId.Value, auditLog.OrganizationId);
				auditLog.UserId = userId.Value;
			}
			else
			{
				var currentUserId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
				if (Guid.TryParse(currentUserId, out var parsedId) && parsedId != Guid.Empty)
				{
					EnsureAuditUserBelongsToOrganization(parsedId, auditLog.OrganizationId);
					auditLog.UserId = parsedId;
				}
			}

			_context.AuditLogs.Add(auditLog);
		}

		private void EnsureAuditUserBelongsToOrganization(Guid userId, Guid organizationId)
		{
			if (userId == Guid.Empty)
				throw new InvalidOperationException("Audit user identity is invalid.");

			// Check tracked users first so a newly-created user cannot be paired with
			// an audit row for another organization before the unit of work is saved.
			var trackedUser = _context.Users.Local.FirstOrDefault(user => user.Id == userId);
			if (trackedUser is not null)
			{
				if (trackedUser.OrganizationId != organizationId)
					throw new InvalidOperationException("Audit user does not belong to the audit organization.");
				return;
			}

			// Ignore the tenant query filter deliberately: this is an ownership check,
			// not a read operation, and must also detect a foreign user under an admin
			// filter-bypass context. A missing user remains subject to the database FK.
			var persistedUser = _context.Users.IgnoreQueryFilters()
				.AsNoTracking()
				.Where(user => user.Id == userId)
			.Select(user => new { user.OrganizationId })
			.SingleOrDefault();
			if (persistedUser is not null && persistedUser.OrganizationId != organizationId)
				throw new InvalidOperationException("Audit user does not belong to the audit organization.");
		}

		public async Task LogUserActionAsync(Guid userId, string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null)
		{
			await LogAsync(action, resourceType, resourceId, oldValues, newValues, userId);
		}
	}
}
