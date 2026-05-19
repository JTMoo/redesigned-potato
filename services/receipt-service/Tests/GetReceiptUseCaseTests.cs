using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReceiptService.Application.UseCases;
using ReceiptService.Data;
using ReceiptService.Domain;

namespace ReceiptService.Tests;

public sealed class GetReceiptUseCaseTests
{
    private static ReceiptDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReceiptDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReceiptDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsReceipt_WhenOwnedByRequestingUser()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        db.Receipts.Add(new Receipt
        {
            Id = receiptId,
            UserId = userId,
            StoreName = "My Store",
            Status = ReceiptStatus.Processed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetReceiptUseCase(db, NullLogger<GetReceiptUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(receiptId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(receiptId);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenReceiptBelongsToDifferentUser()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var ownerUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        db.Receipts.Add(new Receipt
        {
            Id = receiptId,
            UserId = ownerUserId,
            StoreName = "Owner Store",
            Status = ReceiptStatus.Processed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetReceiptUseCase(db, NullLogger<GetReceiptUseCase>.Instance);

        // Act — requesting user is NOT the owner
        var result = await sut.ExecuteAsync(receiptId, attackerUserId);

        // Assert — must return null (caller maps to 404)
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenReceiptDoesNotExist()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var sut = new GetReceiptUseCase(db, NullLogger<GetReceiptUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }
}
