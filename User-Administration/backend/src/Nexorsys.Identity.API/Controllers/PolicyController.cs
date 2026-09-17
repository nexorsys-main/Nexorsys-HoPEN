using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using System;
using System.Threading.Tasks;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/policies")]
	[Authorize(Policy = "AdminOnly")]
	public class PolicyController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organizationContext;
		private readonly Nexorsys.Identity.API.Services.ILicenseEntitlementGuard _licenseGuard;

		public PolicyController(AppDbContext dbContext, IOrganizationContext organizationContext,
			Nexorsys.Identity.API.Services.ILicenseEntitlementGuard licenseGuard)
		{
			_dbContext = dbContext;
			_organizationContext = organizationContext;
			_licenseGuard = licenseGuard;
		}

		[HttpGet]
		public async Task<IActionResult> GetPolicies()
		{
			if (!await HasIdentityEntitlementAsync()) return Forbid();
			var organizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			var policies = await _dbContext.DepartmentPolicies.Where(p => p.OrganizationId == organizationId).ToListAsync();
			return Ok(policies);
		}

		[HttpPost]
		public async Task<IActionResult> CreatePolicy([FromBody] DepartmentPolicy policy)
		{
			if (!await HasIdentityEntitlementAsync()) return Forbid();
			policy.Id = Guid.NewGuid();
			policy.OrganizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			_dbContext.DepartmentPolicies.Add(policy);
			_dbContext.AuditLogs.Add(CreateAudit("Department policy created", policy, null, Snapshot(policy)));
			await _dbContext.SaveChangesAsync();
			return Ok(policy);
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] DepartmentPolicy updatedPolicy)
		{
			if (!await HasIdentityEntitlementAsync()) return Forbid();
			var organizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			var policy = await _dbContext.DepartmentPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId);
			if (policy == null) return NotFound();
			var oldValues = Snapshot(policy);

			policy.Department = updatedPolicy.Department;
			policy.RequiredRiskLevel = updatedPolicy.RequiredRiskLevel;
			policy.AllowedAccessStart = updatedPolicy.AllowedAccessStart;
			policy.AllowedAccessEnd = updatedPolicy.AllowedAccessEnd;
			policy.AllowedAuthenticationMethods = updatedPolicy.AllowedAuthenticationMethods;
			policy.RequiredAssuranceLevel = updatedPolicy.RequiredAssuranceLevel;
			policy.AuthorizedWorkstations = updatedPolicy.AuthorizedWorkstations;
			policy.SessionTimeoutMinutes = updatedPolicy.SessionTimeoutMinutes;

			_dbContext.AuditLogs.Add(CreateAudit("Department policy updated", policy, oldValues, Snapshot(policy)));
			await _dbContext.SaveChangesAsync();
			return Ok(policy);
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> DeletePolicy(Guid id)
		{
			if (!await HasIdentityEntitlementAsync()) return Forbid();
			var organizationId = _organizationContext.OrganizationId.GetValueOrDefault();
			var policy = await _dbContext.DepartmentPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId);
			if (policy == null) return NotFound();

			_dbContext.AuditLogs.Add(CreateAudit("Department policy deleted", policy, Snapshot(policy), null));
			_dbContext.DepartmentPolicies.Remove(policy);
			await _dbContext.SaveChangesAsync();
			return NoContent();
		}

		private async Task<bool> HasIdentityEntitlementAsync()
		{
			if (!_organizationContext.OrganizationId.HasValue || _organizationContext.OrganizationId.Value == Guid.Empty)
				return false;
			var decision = await _licenseGuard.HasFeatureAsync(_organizationContext.OrganizationId.Value, "identity", CancellationToken.None);
			return decision.Allowed;
		}

		private AuditLog CreateAudit(string action, DepartmentPolicy policy, string? oldValues, string? newValues) => new()
		{
			Id = Guid.NewGuid(), OrganizationId = policy.OrganizationId, Action = action,
			ResourceType = "DepartmentPolicy", ResourceId = policy.Id,
			OldValues = Nexorsys.Identity.Infrastructure.AuditRedactor.Redact(oldValues),
			NewValues = Nexorsys.Identity.Infrastructure.AuditRedactor.Redact(newValues),
			IpAddress = ControllerContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
			UserAgent = "api", CreatedAt = DateTime.UtcNow,
			UserId = GetActor(policy.OrganizationId)
		};

		private Guid? GetActor(Guid organizationId)
		{
			var actor = Nexorsys.Identity.API.Services.TenantActorOwnership.ParseAndEnsure(_dbContext,
				ControllerContext?.HttpContext?.User ?? User, organizationId);
			return actor;
		}

		private static string Snapshot(DepartmentPolicy policy) =>
			$"Department={policy.Department}; RequiredRiskLevel={policy.RequiredRiskLevel}; " +
			$"AllowedAccessStart={policy.AllowedAccessStart:c}; AllowedAccessEnd={policy.AllowedAccessEnd:c}; " +
			$"AllowedAuthenticationMethods={policy.AllowedAuthenticationMethods}; " +
			$"RequiredAssuranceLevel={policy.RequiredAssuranceLevel}; AuthorizedWorkstations={policy.AuthorizedWorkstations}; " +
			$"SessionTimeoutMinutes={policy.SessionTimeoutMinutes}";
	}
}
