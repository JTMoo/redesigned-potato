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
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.UserId == ownerUserId);
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
        result[0].StoreName.Should().Be("New Store");
        result[1].StoreName.Should().Be("Old Store");
    }
}
