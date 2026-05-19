using EventContracts.Events;
using FluentAssertions;
using Xunit;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UserService.Application.UseCases;
using UserService.Data;
using UserService.Presentation;
using Utilities;

namespace UserService.Tests;

public sealed class UsersControllerTests
{
    private static UserDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Mock<IDateTimeProvider> FixedClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(c => c.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        return m;
    }

    private static UsersController BuildController(
        UserDbContext db,
        IPublishEndpoint publish,
        IDateTimeProvider clock)
    {
        var upsert = new UpsertUserUseCase(db, publish, clock, NullLogger<UpsertUserUseCase>.Instance);
        var getUser = new GetUserUseCase(db, NullLogger<GetUserUseCase>.Instance);
        var updatePrefs = new UpdatePreferencesUseCase(db, NullLogger<UpdatePreferencesUseCase>.Instance);
        return new UsersController(upsert, getUser, updatePrefs);
    }

    private static UsersController WithUserIdHeader(UsersController controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers["X-User-Id"] = userId.ToString();
        return controller;
    }

    [Fact]
    public async Task Upsert_NewUser_Returns201AndPublishesEvent()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);

        // Act
        var result = await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"), default);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        publish.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_ExistingUser_Returns200AndNoEvent()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"), default);
        publish.Invocations.Clear();

        // Act
        var result = await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Updated Name"), default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        publish.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMe_ExistingUser_ReturnsOk()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = FixedClock();
        var sut = BuildController(db, publish.Object, clock.Object);

        // Create a user first
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"), default);
        var userId = db.Users.First().Id;

        WithUserIdHeader(sut, userId);

        // Act
        var result = await sut.GetMe(default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMe_UnknownUser_Returns404()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);
        WithUserIdHeader(sut, Guid.NewGuid());

        // Act
        var result = await sut.GetMe(default);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMe_MissingHeader_Returns400()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);
        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = await sut.GetMe(default);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PutPreferences_ValidInput_ReturnsOk()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"), default);
        var userId = db.Users.First().Id;
        WithUserIdHeader(sut, userId);

        var body = new List<PreferenceRequest> { new("theme", "dark") };

        // Act
        var result = await sut.PutPreferences(userId, body, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PutPreferences_WrongUser_Returns404()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = BuildController(db, publish.Object, FixedClock().Object);
        await sut.Upsert(new UpsertUserRequest("google-123", "test@example.com", "Test User"), default);
        var userId = db.Users.First().Id;

        // Log in as a different user
        WithUserIdHeader(sut, Guid.NewGuid());
        var body = new List<PreferenceRequest> { new("theme", "dark") };

        // Act
        var result = await sut.PutPreferences(userId, body, default);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
