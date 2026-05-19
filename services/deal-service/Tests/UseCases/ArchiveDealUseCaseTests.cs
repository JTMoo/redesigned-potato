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

public sealed class ArchiveDealUseCaseTests
{
    private static DealDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealDbContext(options);
    }

    private static async Task<Deal> SeedDealAsync(DealDbContext db, bool isActive = true)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Title = "Test Deal",
            Description = "Test Desc",
            DiscountAmount = 10m,
            LocationZip = "10001",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDeal_SetsIsActiveFalse()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var seeded = await SeedDealAsync(db);
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new ArchiveDealUseCase(db, publish.Object, clock.Object, NullLogger<ArchiveDealUseCase>.Instance);

        // Act
        var found = await sut.ExecuteAsync(seeded.Id);

        // Assert
        found.Should().BeTrue();
        var stored = await db.Deals.FindAsync(seeded.Id);
        stored!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDeal_PublishesDealArchivedEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var seeded = await SeedDealAsync(db);
        DealArchivedEvent? publishedEvent = null;
        var publish = new Mock<IPublishEndpoint>();
        publish
            .Setup(p => p.Publish(It.IsAny<DealArchivedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DealArchivedEvent, CancellationToken>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new ArchiveDealUseCase(db, publish.Object, clock.Object, NullLogger<ArchiveDealUseCase>.Instance);

        // Act
        await sut.ExecuteAsync(seeded.Id);

        // Assert
        publishedEvent.Should().NotBeNull();
        publishedEvent!.DealId.Should().Be(seeded.Id);
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_ReturnsFalse()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new ArchiveDealUseCase(db, publish.Object, clock.Object, NullLogger<ArchiveDealUseCase>.Instance);

        // Act
        var found = await sut.ExecuteAsync(Guid.NewGuid());

        // Assert
        found.Should().BeFalse();
        publish.Verify(
            p => p.Publish(It.IsAny<DealArchivedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_DoesNotPublishEvent()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var publish = new Mock<IPublishEndpoint>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        var sut = new ArchiveDealUseCase(db, publish.Object, clock.Object, NullLogger<ArchiveDealUseCase>.Instance);

        // Act
        await sut.ExecuteAsync(Guid.NewGuid());

        // Assert
        publish.VerifyNoOtherCalls();
    }
}
