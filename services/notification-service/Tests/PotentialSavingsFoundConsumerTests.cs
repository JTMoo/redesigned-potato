using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationService.Application.Consumers;
using NotificationService.Data;
using Utilities;

namespace NotificationService.Tests;

public sealed class PotentialSavingsFoundConsumerTests
{
    private static NotificationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Consume_CreatesNotificationWithCorrectMessageFormat()
    {
        // Arrange
        var db = CreateDb();
        var clock = new Mock<IDateTimeProvider>();
        var now = DateTimeOffset.UtcNow;
        clock.Setup(c => c.UtcNow).Returns(now);
        var sut = new PotentialSavingsFoundConsumer(db, clock.Object, NullLogger<PotentialSavingsFoundConsumer>.Instance);

        var userId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var evt = new PotentialSavingsFoundEvent(userId, receiptId, "SuperMart", 3, 5.50m);

        var consumeContext = new Mock<ConsumeContext<PotentialSavingsFoundEvent>>();
        consumeContext.Setup(c => c.Message).Returns(evt);

        // Act
        await sut.Consume(consumeContext.Object);

        // Assert
        var log = await db.Logs.SingleAsync();
        log.Message.Should().Be("We found 3 deal(s) matching your receipt from SuperMart!");
    }

    [Fact]
    public async Task Consume_SetsCorrectUserIdAndReceiptId()
    {
        // Arrange
        var db = CreateDb();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new PotentialSavingsFoundConsumer(db, clock.Object, NullLogger<PotentialSavingsFoundConsumer>.Instance);

        var userId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var evt = new PotentialSavingsFoundEvent(userId, receiptId, "QuickShop", 1, 2.00m);

        var consumeContext = new Mock<ConsumeContext<PotentialSavingsFoundEvent>>();
        consumeContext.Setup(c => c.Message).Returns(evt);

        // Act
        await sut.Consume(consumeContext.Object);

        // Assert
        var log = await db.Logs.SingleAsync();
        log.UserId.Should().Be(userId.ToString());
        log.ReceiptId.Should().Be(receiptId);
        log.IsRead.Should().BeFalse();
        log.CreatedAt.Should().Be(clock.Object.UtcNow);
    }

    [Fact]
    public async Task Consume_AssignsNewUniqueId()
    {
        // Arrange
        var db = CreateDb();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new PotentialSavingsFoundConsumer(db, clock.Object, NullLogger<PotentialSavingsFoundConsumer>.Instance);

        var userId = Guid.NewGuid();
        var evt = new PotentialSavingsFoundEvent(userId, Guid.NewGuid(), "Store", 2, 3.00m);
        var consumeContext = new Mock<ConsumeContext<PotentialSavingsFoundEvent>>();
        consumeContext.Setup(c => c.Message).Returns(evt);

        // Act
        await sut.Consume(consumeContext.Object);

        // Assert
        var log = await db.Logs.SingleAsync();
        log.Id.Should().NotBe(Guid.Empty);
    }
}
