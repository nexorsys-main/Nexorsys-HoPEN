using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class RiskBasedAuthService
	{
		private readonly AppDbContext _dbContext;

		public RiskBasedAuthService(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<string> DetermineRequiredAssuranceLevelAsync(User user, Workstation workstation)
		{
			if (user.OrganizationId == Guid.Empty || workstation.OrganizationId == Guid.Empty ||
				user.OrganizationId != workstation.OrganizationId)
				return "Blocked_TenantMismatch";

			var policy = await _dbContext.DepartmentPolicies.FirstOrDefaultAsync(p =>
				p.OrganizationId == user.OrganizationId && p.Department == workstation.Department);

			if (policy != null)
			{
				var nowTime = DateTime.UtcNow.TimeOfDay;
				if (nowTime < policy.AllowedAccessStart || nowTime > policy.AllowedAccessEnd)
				{
					return "Blocked_TimeRestriction";
				}

				return policy.RequiredRiskLevel;
			}

			return "Low"; // Default fallback
		}
	}
}
