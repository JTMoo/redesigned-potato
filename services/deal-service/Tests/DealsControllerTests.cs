using DealService.Application.DTOs;
using DealService.Application.UseCases;
using DealService.Data;
using DealService.Presentation;
using EventContracts.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Utilities;
using Xunit;

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

    private static DealsController CreateController(DealDbContext db, Mock<IPublishEndpoint> publish, Mock<IDateTimeProvider> clock)
    {
        var createUseCase = new CreateDealUseCase(db, publish.Object, clock.Object, NullLogger<CreateDealUseCase>.Instance);
        var listUseCase = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);
        var updateUseCase = new UpdateDealUseCase(db, publish.Object, clock.Object, NullLogger<UpdateDealUseCase>.Instance);
        var archiveUseCase = new ArchiveDealUseCase(db, publish.Object, clock.Object, NullLogger<ArchiveDealUseCase>.Instance);
        return new DealsController(createUseCase, listUseCase, updateUseCase, archiveUseCase, db);
    }

    [Fact]
    public async Task Create_PublishesDealCreatedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = CreateController(db, publish, clock);

        // Act
        await sut.Create(new CreateDealRequest("10% off", "All items", 10m, null));

        // Assert
        publish.Verify(p => p.Publish(It.IsAny<DealCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Archive_SetsIsActiveFalse_AndPublishesArchivedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = CreateController(db, publish, clock);
        var createResult = (CreatedAtActionResult)(await sut.Create(new CreateDealRequest("Deal", "Desc", 5m, "10001")));
        var deal = (DealService.Domain.Deal)createResult.Value!;

        // Act
        await sut.Archive(deal.Id);

        // Assert
        var stored = await db.Deals.FindAsync(deal.Id);
        stored!.IsActive.Should().BeFalse();
        publish.Verify(p => p.Publish(It.IsAny<DealArchivedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = CreateController(db, publish, clock);

        // Act
        var result = await sut.GetById(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = CreateController(db, publish, clock);

        // Act
        var result = await sut.Update(Guid.NewGuid(), new UpdateDealRequest("T", "D", 5m, null));

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAll_WithZipFilter_ReturnsFilteredDeals()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);
        var sut = CreateController(db, publish, clock);

        await sut.Create(new CreateDealRequest("Deal 1", "Desc", 5m, "10001"));
        await sut.Create(new CreateDealRequest("Deal 2", "Desc", 10m, "90210"));

        // Act
        var result = (OkObjectResult)(await sut.GetAll(zip: "10001"));
        var paged = (PagedResult<DealService.Domain.Deal>)result.Value!;

        // Assert
        // Only deals matching zip 10001 or with no zip should be returned
        paged.Items.Should().NotContain(d => d.LocationZip == "90210");
    }
}
