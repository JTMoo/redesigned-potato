using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.UseCases;
using NotificationService.Data;
using NotificationService.Domain;

namespace NotificationService.Tests;

public sealed class MarkNotificationReadUseCaseTests
{
    private static NotificationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Execute_SetsIsReadToTrue()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var notificationId = Guid.NewGuid();

        db.Logs.Add(new NotificationLog
        {
            Id = notificationId,
            UserId = userId,
            ReceiptId = Guid.NewGuid(),
            Message = "You have a deal!",
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new MarkNotificationReadUseCase(db, NullLogger<MarkNotificationReadUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(notificationId, userId);

        // Assert
        result.Should().BeTrue();
        var updated = await db.Logs.FindAsync(notificationId);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsFalse_WhenNotificationBelongsToDifferentUser()
    {
        // Arrange
        var db = CreateDb();
        var ownerUserId = Guid.NewGuid().ToString();
        var requestingUserId = Guid.NewGuid().ToString();
        var notificationId = Guid.NewGuid();

        db.Logs.Add(new NotificationLog
        {
            Id = notificationId,
            UserId = ownerUserId,
            ReceiptId = Guid.NewGuid(),
            Message = "You have a deal!",
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new MarkNotificationReadUseCase(db, NullLogger<MarkNotificationReadUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(notificationId, requestingUserId);

        // Assert
        result.Should().BeFalse();
        var unchanged = await db.Logs.FindAsync(notificationId);
        unchanged!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ReturnsFalse_WhenNotificationDoesNotExist()
    {
        // Arrange
        var db = CreateDb();
        var sut = new MarkNotificationReadUseCase(db, NullLogger<MarkNotificationReadUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid().ToString());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_WhenAlreadyRead_StillReturnsTrue()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var notificationId = Guid.NewGuid();

        db.Logs.Add(new NotificationLog
        {
            Id = notificationId,
            UserId = userId,
            ReceiptId = Guid.NewGuid(),
            Message = "Already read",
            IsRead = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new MarkNotificationReadUseCase(db, NullLogger<MarkNotificationReadUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(notificationId, userId);

        // Assert
        result.Should().BeTrue();
    }
}
