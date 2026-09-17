using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nexorsys.Identity.API.Controllers;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;
using Xunit;

namespace Nexorsys.Identity.API.Tests;

public class SetupControllerTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IAuditService> _mockAudit;
    private readonly Mock<ILogger<SetupController>> _mockLogger;
    private readonly SetupController _controller;

    public SetupControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockOrgContext = new Mock<IOrganizationContext>();
        mockOrgContext.Setup(o => o.OrganizationId).Returns(Guid.NewGuid());
        mockOrgContext.Setup(o => o.IsSystemAdministrator).Returns(true);

        _dbContext = new AppDbContext(options, mockOrgContext.Object);
        _mockAudit = new Mock<IAuditService>();
        _mockLogger = new Mock<ILogger<SetupController>>();
        _controller = new SetupController(_dbContext, _mockAudit.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InitializeAdmin_FailsIfAlreadyInitialized()
    {
        // Arrange
        _dbContext.Users.Add(new User { Id = Guid.NewGuid(), SamAccountName = "admin", IsLocalProfile = true, PasswordHash = "already_set" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.InitializeAdmin(new SetupController.SetupRequest { Username = "admin", Password = "new_password" });

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task InitializeAdmin_FailsIfUserNotFound()
    {
        // Act
        var result = await _controller.InitializeAdmin(new SetupController.SetupRequest { Username = "admin", Password = "new_password" });

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Administrator profile not found.", notFoundResult.Value);
    }

    [Fact]
    public async Task InitializeAdmin_SucceedsAndHashesPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new User { Id = userId, SamAccountName = "admin", IsLocalProfile = true, PasswordHash = null });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.InitializeAdmin(new SetupController.SetupRequest { Username = "admin", Password = "strong_password" });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var user = await _dbContext.Users.FindAsync(userId);
        Assert.NotNull(user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("strong_password", user.PasswordHash));
        
        _mockAudit.Verify(a => a.LogAsync("System Initialization: Admin Password Set", "System", userId, null, "PasswordHash=***", null), Times.Once);
    }
}
