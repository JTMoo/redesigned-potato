using DealService.Application.UseCases;
using DealService.Data;
using DealService.Domain;
using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Utilities;
using Xunit;

namespace DealService.Tests.UseCases;

public sealed class UpdateDealUseCaseTests
{
    private static DealDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealDbContext(options);
    }

    private static async Task<Deal> SeedDealAsync(DealDbContext db)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Title = "Old Title",
            Description = "Old Desc",
            DiscountAmount = 5m,
            LocationZip = "10001",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDeal_UpdatesFields()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var seeded = await SeedDealAsync(db);
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new UpdateDealUseCase(db, publish.Object, clock.Object, NullLogger<UpdateDealUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(seeded.Id, "New Title", "New Desc", 20m, "90210");

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("New Title");
        result.Description.Should().Be("New Desc");
        result.DiscountAmount.Should().Be(20m);
        result.LocationZip.Should().Be("90210");
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDeal_PublishesDealUpdatedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var seeded = await SeedDealAsync(db);
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new UpdateDealUseCase(db, publish.Object, clock.Object, NullLogger<UpdateDealUseCase>.Instance);

        // Act
        await sut.ExecuteAsync(seeded.Id, "Title", "Desc", 10m, null);

        // Assert
        publish.Verify(
            p => p.Publish(It.IsAny<DealUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new UpdateDealUseCase(db, publish.Object, clock.Object, NullLogger<UpdateDealUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(Guid.NewGuid(), "Title", "Desc", 10m, null);

        // Assert
        result.Should().BeNull();
        publish.Verify(
            p => p.Publish(It.IsAny<DealUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
