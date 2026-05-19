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
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(n => n.UserId == userId);
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
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(unreadId, "unread notification should appear first");
        result.Items[1].Id.Should().Be(readId);
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
        result.Items[0].Id.Should().Be(newerUnreadId, "newer unread notification should appear first");
        result.Items[1].Id.Should().Be(olderUnreadId);
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
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_Page2_ReturnsSecondPageItems()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            db.Logs.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReceiptId = Guid.NewGuid(),
                Message = $"Msg {i}",
                IsRead = false,
                CreatedAt = now.AddMinutes(-i),
            });
        }
        await db.SaveChangesAsync();

        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var page1 = await sut.ExecuteAsync(userId, page: 1, pageSize: 3);
        var page2 = await sut.ExecuteAsync(userId, page: 2, pageSize: 3);

        // Assert
        page1.Items.Should().HaveCount(3);
        page1.TotalCount.Should().Be(5);
        page1.Page.Should().Be(1);

        page2.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(5);
        page2.Page.Should().Be(2);

        page1.Items.Select(n => n.Id).Should().NotIntersectWith(page2.Items.Select(n => n.Id));
    }

    [Fact]
    public async Task Execute_PageSizeClamped_WhenExceedsMax()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid().ToString();
        db.Logs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReceiptId = Guid.NewGuid(),
            Message = "Msg",
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetNotificationsUseCase(db, NullLogger<GetNotificationsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId, page: 1, pageSize: 500);

        // Assert
        result.PageSize.Should().Be(100);
    }
}
