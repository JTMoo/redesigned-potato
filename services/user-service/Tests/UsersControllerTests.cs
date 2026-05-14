using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UserService.Controllers;
using UserService.Data;
using Utilities;

namespace UserService.Tests;

public sealed class UsersControllerTests
{
    private static UserDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    [Fact]
    public async Task Upsert_NewUser_ReturnsUserIdAndPublishesEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new UsersController(db, publishEndpoint.Object, clock.Object);

        // Act
        var result = await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        publishEndpoint.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), default), Times.Once);
    }

    [Fact]
    public async Task Upsert_ExistingUser_DoesNotPublishEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new UsersController(db, publishEndpoint.Object, clock.Object);
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"));
        publishEndpoint.Invocations.Clear();

        // Act
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Updated Name"));

        // Assert
        publishEndpoint.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), default), Times.Never);
    }
}
