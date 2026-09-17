using Microsoft.AspNetCore.Http;
using Moq;
using Nexorsys.Identity.API.Services;
using System.Security.Claims;
using Xunit;

namespace Nexorsys.Identity.API.Tests;

public class TenantIsolationTests
{
    [Fact]
    public void OrganizationContext_IgnoresXOrganizationIdHeader_WhenUserIsAuthenticated()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        
        var authenticatedOrgId = Guid.NewGuid();
        var maliciousSpoofedOrgId = Guid.NewGuid();

        // The user's valid JWT token establishes their organization via the "org_id" claim
        var claims = new[] { new Claim("org_id", authenticatedOrgId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // A malicious actor attempts to spoof the header
        context.Request.Headers["X-Organization-Id"] = maliciousSpoofedOrgId.ToString();
        context.Request.Path = "/api/users"; // Protected endpoint

        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        var organizationContext = new OrganizationContext(mockHttpContextAccessor.Object);

        // Act
        var resolvedOrgId = organizationContext.OrganizationId;

        // Assert
        Assert.Equal(authenticatedOrgId, resolvedOrgId);
        Assert.NotEqual(maliciousSpoofedOrgId, resolvedOrgId);
    }

    [Fact]
    public void OrganizationContext_AcceptsXOrganizationId_OnlyOnLogin()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        
        var targetOrgId = Guid.NewGuid();

        // Unauthenticated request
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        
        context.Request.Headers["X-Organization-Id"] = targetOrgId.ToString();
        context.Request.Path = "/api/auth/login"; 

        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        var organizationContext = new OrganizationContext(mockHttpContextAccessor.Object);

        // Act
        var resolvedOrgId = organizationContext.OrganizationId;

        // Assert
        Assert.Equal(targetOrgId, resolvedOrgId);
    }
    
    [Fact]
    public void OrganizationContext_IgnoresXOrganizationId_OnOtherUnauthenticatedEndpoints()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        
        var targetOrgId = Guid.NewGuid();

        // Unauthenticated request
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        
        context.Request.Headers["X-Organization-Id"] = targetOrgId.ToString();
        context.Request.Path = "/api/setup/initialize"; 

        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

        var organizationContext = new OrganizationContext(mockHttpContextAccessor.Object);

        // Act
        var resolvedOrgId = organizationContext.OrganizationId;

        // Assert
        Assert.Null(resolvedOrgId);
    }
}
