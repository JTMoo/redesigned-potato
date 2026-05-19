using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReceiptService.Application.UseCases;
using ReceiptService.Data;
using ReceiptService.Domain;

namespace ReceiptService.Tests;

public sealed class GetReceiptsUseCaseTests
{
    private static ReceiptDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReceiptDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReceiptDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyReceiptsBelongingToRequestingUser()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.Receipts.AddRange(
            new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                StoreName = "My Store",
                Status = ReceiptStatus.Processed,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                StoreName = "Another Store",
                Status = ReceiptStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                StoreName = "Other User Store",
                Status = ReceiptStatus.Processed,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var sut = new GetReceiptsUseCase(db, NullLogger<GetReceiptsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(ownerUserId);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(r => r.UserId == ownerUserId);
    }

    [Fact]
    public async Task ExecuteAsync_OrdersReceiptsByCreatedAtDescending()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var older = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = DateTimeOffset.UtcNow;

        db.Receipts.AddRange(
            new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StoreName = "Old Store",
                Status = ReceiptStatus.Processed,
                CreatedAt = older,
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StoreName = "New Store",
                Status = ReceiptStatus.Processed,
                CreatedAt = newer,
            }
        );
        await db.SaveChangesAsync();

        var sut = new GetReceiptsUseCase(db, NullLogger<GetReceiptsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId);

        // Assert — newest first
        result.Items[0].StoreName.Should().Be("New Store");
        result.Items[1].StoreName.Should().Be("Old Store");
    }

    [Fact]
    public async Task ExecuteAsync_Page2_ReturnsSecondPageItems()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            db.Receipts.Add(new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StoreName = $"Store {i}",
                Status = ReceiptStatus.Processed,
                CreatedAt = baseTime.AddMinutes(-i),
            });
        }
        await db.SaveChangesAsync();

        var sut = new GetReceiptsUseCase(db, NullLogger<GetReceiptsUseCase>.Instance);

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

        page1.Items.Select(r => r.Id).Should().NotIntersectWith(page2.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeClamped_WhenExceedsMax()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StoreName = "Store",
            Status = ReceiptStatus.Processed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetReceiptsUseCase(db, NullLogger<GetReceiptsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(userId, page: 1, pageSize: 500);

        // Assert
        result.PageSize.Should().Be(100);
    }
}
