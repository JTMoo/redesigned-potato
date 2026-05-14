using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationService.Data;
using NotificationService.Events;
using Utilities;

namespace NotificationService.Tests;

public sealed class UserCreatedConsumerTests
{
    [Fact]
    public async Task Consume_UserCreatedEvent_CreatesEmailSubscription()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationDbContext(options);
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new UserCreatedConsumer(db, clock.Object, NullLogger<UserCreatedConsumer>.Instance);

        var userId = Guid.NewGuid();
        var consumeContext = new Mock<ConsumeContext<UserCreatedEvent>>();
        consumeContext.Setup(c => c.Message).Returns(
            new UserCreatedEvent(userId, "test@example.com", "Test User", DateTimeOffset.UtcNow));

        // Act
        await sut.Consume(consumeContext.Object);

        // Assert
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        subscription.Should().NotBeNull();
        subscription!.Channel.Should().Be("email");
        subscription.IsActive.Should().BeTrue();
    }
}
