using DealService.Controllers;
using DealService.Data;
using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Utilities;

namespace DealService.Tests;

public sealed class DealsControllerTests
{
    private static DealDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealDbContext(options);
    }

    [Fact]
    public async Task Create_PublishesDealCreatedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new DealsController(db, publish.Object, clock.Object);

        // Act
        await sut.Create(new CreateDealRequest("10% off", "All items", 10m, null));

        // Assert
        publish.Verify(p => p.Publish(It.IsAny<DealCreatedEvent>(), default), Times.Once);
    }

    [Fact]
    public async Task Archive_SetsIsActiveFalse_AndPublishesArchivedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = new DealsController(db, publish.Object, clock.Object);
        var createResult = (CreatedAtActionResult)(await sut.Create(new CreateDealRequest("Deal", "Desc", 5m, "10001")));
        var deal = (DealService.Domain.Deal)createResult.Value!;

        // Act
        await sut.Archive(deal.Id);

        // Assert
        var stored = await db.Deals.FindAsync(deal.Id);
        stored!.IsActive.Should().BeFalse();
        publish.Verify(p => p.Publish(It.IsAny<DealArchivedEvent>(), default), Times.Once);
    }
}
