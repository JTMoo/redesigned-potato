using EventContracts.Events;
using FluentAssertions;
using Xunit;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UserService.Application.Exceptions;
using UserService.Application.UseCases;
using UserService.Data;
using UserService.Domain;
using Utilities;

namespace UserService.Tests;

public sealed class UpsertUserUseCaseTests
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

    [Fact]
    public async Task ExecuteAsync_NewUser_ReturnsUserAndPublishesEvent()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = new UpsertUserUseCase(db, publish.Object, FixedClock().Object, NullLogger<UpsertUserUseCase>.Instance);

        // Act
        var (user, wasCreated) = await sut.ExecuteAsync("g-1", "a@b.com", "Alice");

        // Assert
        wasCreated.Should().BeTrue();
        user.Email.Should().Be("a@b.com");
        user.DisplayName.Should().Be("Alice");
        publish.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingUser_ReturnsUpdatedUserAndNoEvent()
    {
        // Arrange
        var db = CreateDb();
        var publish = new Mock<IPublishEndpoint>();
        var sut = new UpsertUserUseCase(db, publish.Object, FixedClock().Object, NullLogger<UpsertUserUseCase>.Instance);
        await sut.ExecuteAsync("g-1", "a@b.com", "Alice");
        publish.Invocations.Clear();

        // Act
        var (user, wasCreated) = await sut.ExecuteAsync("g-1", "a@b.com", "Alice Updated");

        // Assert
        wasCreated.Should().BeFalse();
        user.DisplayName.Should().Be("Alice Updated");
        publish.Verify(p => p.Publish(It.IsAny<UserCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed class GetUserUseCaseTests
{
    private static UserDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ExecuteAsync_ExistingUser_ReturnsDto()
    {
        // Arrange
        var db = CreateDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = "g-1",
            Email = "a@b.com",
            DisplayName = "Alice",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = new GetUserUseCase(db, NullLogger<GetUserUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(user.Id);

        // Assert
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task ExecuteAsync_MissingUser_ThrowsNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var sut = new GetUserUseCase(db, NullLogger<GetUserUseCase>.Instance);

        // Act
        Func<Task> act = () => sut.ExecuteAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public sealed class UpdatePreferencesUseCaseTests
{
    private static UserDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> SeedUser(UserDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = "g-1",
            Email = "a@b.com",
            DisplayName = "Alice",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_ReplacesPreferences()
    {
        // Arrange
        var db = CreateDb();
        var user = await SeedUser(db);
        // Pre-seed an existing preference that should be replaced
        db.UserPreferences.Add(new UserPreference
        {
            Id = Guid.NewGuid(), UserId = user.Id, PreferenceKey = "old", Value = "value"
        });
        await db.SaveChangesAsync();

        var sut = new UpdatePreferencesUseCase(db, NullLogger<UpdatePreferencesUseCase>.Instance);
        var inputs = new List<PreferenceInput>
        {
            new("theme", "dark"),
            new("language", "en"),
        };

        // Act
        var result = await sut.ExecuteAsync(user.Id, user.Id, inputs);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(p => p.PreferenceKey == "theme" && p.Value == "dark");
        result.Should().ContainSingle(p => p.PreferenceKey == "language" && p.Value == "en");

        // Old preference should be gone
        db.UserPreferences.Should().NotContain(p => p.PreferenceKey == "old");
    }

    [Fact]
    public async Task ExecuteAsync_WrongUser_ThrowsNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var user = await SeedUser(db);
        var differentUserId = Guid.NewGuid();

        var sut = new UpdatePreferencesUseCase(db, NullLogger<UpdatePreferencesUseCase>.Instance);

        // Act
        Func<Task> act = () => sut.ExecuteAsync(user.Id, differentUserId, new List<PreferenceInput>());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetForUserAsync_ExistingUser_ReturnsPreferences()
    {
        // Arrange
        var db = CreateDb();
        var user = await SeedUser(db);
        db.UserPreferences.Add(new UserPreference
        {
            Id = Guid.NewGuid(), UserId = user.Id, PreferenceKey = "theme", Value = "light"
        });
        await db.SaveChangesAsync();

        var sut = new UpdatePreferencesUseCase(db, NullLogger<UpdatePreferencesUseCase>.Instance);

        // Act
        var result = await sut.GetForUserAsync(user.Id, user.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].PreferenceKey.Should().Be("theme");
    }

    [Fact]
    public async Task GetForUserAsync_WrongUser_ThrowsNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var user = await SeedUser(db);
        var sut = new UpdatePreferencesUseCase(db, NullLogger<UpdatePreferencesUseCase>.Instance);

        // Act
        Func<Task> act = () => sut.GetForUserAsync(user.Id, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
