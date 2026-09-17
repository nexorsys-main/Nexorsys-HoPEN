using System.Security.Claims;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services;

public sealed class OrganizationContext : IOrganizationContext
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public OrganizationContext(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public Guid? OrganizationId
	{
		get
		{
			var httpContext = _httpContextAccessor.HttpContext;
			// Organization context is an authorization claim, not a client-selectable header.
			// Device endpoints must resolve tenant ownership from the certificate-bound workstation.
			var value = httpContext?.User.FindFirstValue("org_id");
			if (string.IsNullOrWhiteSpace(value) && httpContext?.Items.TryGetValue("AuthenticatedOrganizationId", out var authenticatedOrg) == true)
				value = authenticatedOrg?.ToString();
			// Login may select a tenant namespace only to locate an already-provisioned
			// identity. Login never creates users or assigns tenant membership.
			if (string.IsNullOrWhiteSpace(value) && httpContext?.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) == true)
				value = httpContext.Request.Headers["X-Organization-Id"].ToString();
			return Guid.TryParse(value, out var organizationId) ? organizationId : null;
		}
	}

	public bool IsSystemAdministrator =>
		_httpContextAccessor.HttpContext?.User.IsInRole("SYSTEM_ADMIN") == true;
}
