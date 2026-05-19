using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.UseCases;
using NotificationService.Data;
using NotificationService.Domain;

namespace NotificationService.Tests;

public sealed class GetNotificationsUseCaseTests
{
    private static NotificationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Execute_ReturnsOnlyNotificationsForRequestingUser()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        db.Logs.AddRange(
            new NotificationLog { Id = Guid.NewGuid(), UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Msg 1", IsRead = false, CreatedAt = now },
            new NotificationLog { Id = Guid.NewGuid(), UserId = otherUserId, ReceiptId = Guid.NewGuid(), Message = "Other user msg", IsRead = false, CreatedAt = now },
            new NotificationLog { Id = Guid.NewGuid(), UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Msg 2", IsRead = true, CreatedAt = now.AddMinutes(-5) }
        );
        await db.SaveChangesAsync();

        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(n => n.UserId == userId);
    }

    [Fact]
    public async Task Execute_ReturnsUnreadBeforeRead()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var readId = Guid.NewGuid();
        var unreadId = Guid.NewGuid();

        db.Logs.AddRange(
            new NotificationLog { Id = readId, UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Read", IsRead = true, CreatedAt = now.AddMinutes(-1) },
            new NotificationLog { Id = unreadId, UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Unread", IsRead = false, CreatedAt = now.AddMinutes(-10) }
        );
        await db.SaveChangesAsync();

        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(unreadId, "unread notification should appear first");
        result[1].Id.Should().Be(readId);
    }

    [Fact]
    public async Task Execute_UnreadNotifications_OrderedByCreatedAtDescending()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var olderUnreadId = Guid.NewGuid();
        var newerUnreadId = Guid.NewGuid();

        db.Logs.AddRange(
            new NotificationLog { Id = olderUnreadId, UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Older unread", IsRead = false, CreatedAt = now.AddMinutes(-10) },
            new NotificationLog { Id = newerUnreadId, UserId = userId, ReceiptId = Guid.NewGuid(), Message = "Newer unread", IsRead = false, CreatedAt = now.AddMinutes(-1) }
        );
        await db.SaveChangesAsync();

        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId);

        // Assert
        result[0].Id.Should().Be(newerUnreadId, "newer unread notification should appear first");
        result[1].Id.Should().Be(olderUnreadId);
    }

    [Fact]
    public async Task Execute_ReturnsEmptyList_WhenNoNotificationsForUser()
    {
        // Arrange
        var db = CreateDb();
        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(Guid.NewGuid().ToString());

        // Assert
        result.Should().BeEmpty();
    }
}
