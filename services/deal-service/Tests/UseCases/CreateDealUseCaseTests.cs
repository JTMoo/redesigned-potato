using DealService.Application.UseCases;
using DealService.Data;
using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Utilities;
using Xunit;

namespace DealService.Tests.UseCases;

public sealed class CreateDealUseCaseTests
{
    private static DealDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_PersistsDeal()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        var now = DateTimeOffset.UtcNow;
        clock.Setup(c => c.UtcNow).Returns(now);

        var sut = new CreateDealUseCase(db, publish.Object, clock.Object, NullLogger<CreateDealUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync("10% off", "All items", 10m, "10001");

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("10% off");
        result.Description.Should().Be("All items");
        result.DiscountAmount.Should().Be(10m);
        result.LocationZip.Should().Be("10001");
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().Be(now);

        var stored = await db.Deals.FindAsync(result.Id);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_PublishesDealCreatedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new CreateDealUseCase(db, publish.Object, clock.Object, NullLogger<CreateDealUseCase>.Instance);

        // Act
        await sut.ExecuteAsync("Deal", "Description", 5m, null);

        // Assert
        publish.Verify(
            p => p.Publish(It.IsAny<DealCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PublishedEvent_ContainsCorrectDealId()
    {
        // Arrange
        var db = CreateInMemoryDb();
        DealCreatedEvent? publishedEvent = null;
        var publish = new Mock<IPublishEndpoint>();
        publish
            .Setup(p => p.Publish(It.IsAny<DealCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DealCreatedEvent, CancellationToken>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new CreateDealUseCase(db, publish.Object, clock.Object, NullLogger<CreateDealUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync("Title", "Desc", 15m, "20001");

        // Assert
        publishedEvent.Should().NotBeNull();
        publishedEvent!.DealId.Should().Be(result.Id);
        publishedEvent.Title.Should().Be("Title");
        publishedEvent.DiscountAmount.Should().Be(15m);
        publishedEvent.LocationZip.Should().Be("20001");
    }
}
