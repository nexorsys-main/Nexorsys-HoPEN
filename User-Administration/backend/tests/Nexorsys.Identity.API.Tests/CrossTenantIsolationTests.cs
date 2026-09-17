using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Nexorsys.Identity.API.Filters;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Nexorsys.Identity.API.Tests
{
    public class CrossTenantIsolationTests
    {
        private ActionExecutingContext CreateContext(
            Guid requestOrgId, Guid requestWorkstationId, string requestApiKey,
            AppDbContext dbContext, bool isDevelopment = true, string configuredApiKey = "dev_key")
        {
            var httpContext = new DefaultHttpContext();
            var services = new ServiceCollection();

            var mockConfig = new Mock<IConfiguration>();
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(a => a.Value).Returns(configuredApiKey);
            mockConfig.Setup(a => a.GetSection("KioskApiKey")).Returns(mockConfigSection.Object);

            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns(isDevelopment ? Environments.Development : Environments.Production);

            var mockLogger = new Mock<ILogger<ApiKeyAuthAttribute>>();

            services.AddSingleton(mockConfig.Object);
            services.AddSingleton(mockEnv.Object);
            services.AddSingleton(dbContext);
            services.AddSingleton(mockLogger.Object);

            httpContext.RequestServices = services.BuildServiceProvider();

            if (!string.IsNullOrEmpty(requestApiKey))
                httpContext.Request.Headers["X-Api-Key"] = requestApiKey;
            if (requestOrgId != Guid.Empty)
                httpContext.Request.Headers["X-Organization-Id"] = requestOrgId.ToString();
            if (requestWorkstationId != Guid.Empty)
                httpContext.Request.Headers["X-Workstation-Id"] = requestWorkstationId.ToString();

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new object()
            );
        }

        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var mockOrgContext = new Mock<Nexorsys.Identity.Core.Abstractions.IOrganizationContext>();
            return new AppDbContext(options, mockOrgContext.Object);
        }

        [Fact]
        public async Task TestA_TenantAKioskCredential_TenantBOrganizationId_Deny()
        {
            // Arrange
            var db = GetDbContext();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var workstationA = Guid.NewGuid();

            db.Workstations.Add(new Workstation
            {
                Id = workstationA,
                OrganizationId = tenantA,
                IsActive = true,
                EnrollmentState = WorkstationLifecycle.Active,
                Hostname = "A"
            });
            await db.SaveChangesAsync();

            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(tenantB, workstationA, "dev_key", db);

            // Act
            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestB_TenantAKioskCredential_TenantBWorkstationId_Deny()
        {
            var db = GetDbContext();
            var tenantA = Guid.NewGuid();
            var workstationB = Guid.NewGuid(); // Belongs to Tenant B

            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(tenantA, workstationB, "dev_key", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestE_UnknownWorkstation_Deny()
        {
            var db = GetDbContext();
            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(Guid.NewGuid(), Guid.NewGuid(), "dev_key", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestF_RevokedWorkstation_Deny()
        {
            var db = GetDbContext();
            var tenantA = Guid.NewGuid();
            var workstationA = Guid.NewGuid();

            db.Workstations.Add(new Workstation
            {
                Id = workstationA,
                OrganizationId = tenantA,
                IsActive = true,
                EnrollmentState = WorkstationLifecycle.Revoked,
                Hostname = "A"
            });
            await db.SaveChangesAsync();

            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(tenantA, workstationA, "dev_key", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestG_DisabledWorkstation_Deny()
        {
            var db = GetDbContext();
            var tenantA = Guid.NewGuid();
            var workstationA = Guid.NewGuid();

            db.Workstations.Add(new Workstation
            {
                Id = workstationA,
                OrganizationId = tenantA,
                IsActive = false,
                EnrollmentState = WorkstationLifecycle.Active,
                Hostname = "A"
            });
            await db.SaveChangesAsync();

            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(tenantA, workstationA, "dev_key", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestI_WrongApiKey_Deny()
        {
            var db = GetDbContext();
            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(Guid.NewGuid(), Guid.NewGuid(), "wrong_key", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }

        [Fact]
        public async Task TestJ_MissingApiKey_Deny()
        {
            var db = GetDbContext();
            var attr = new ApiKeyAuthAttribute();
            var context = CreateContext(Guid.NewGuid(), Guid.NewGuid(), "", db);

            await attr.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null)));

            Assert.IsType<UnauthorizedObjectResult>(context.Result);
        }
    }
}
