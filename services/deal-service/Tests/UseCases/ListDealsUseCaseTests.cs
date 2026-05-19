using DealService.Application.UseCases;
using DealService.Data;
using DealService.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DealService.Tests.UseCases;

public sealed class ListDealsUseCaseTests
{
    private static DealDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<DealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DealDbContext(options);
    }

    private static Deal MakeDeal(bool isActive, string? zip = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Deal",
        Description = "Desc",
        DiscountAmount = 5m,
        LocationZip = zip,
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyActiveDeals()
    {
        // Arrange
        var db = CreateInMemoryDb();
        db.Deals.AddRange(MakeDeal(isActive: true), MakeDeal(isActive: false), MakeDeal(isActive: true));
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task ExecuteAsync_WithZipFilter_ReturnsMatchingZipAndNullZipDeals()
    {
        // Arrange
        var db = CreateInMemoryDb();
        db.Deals.AddRange(
            MakeDeal(isActive: true, zip: "10001"),   // matches
            MakeDeal(isActive: true, zip: null),       // matches (no zip = applies broadly)
            MakeDeal(isActive: true, zip: "90210"),    // does NOT match
            MakeDeal(isActive: false, zip: "10001")    // inactive → excluded
        );
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(zip: "10001");

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(d => d.LocationZip == "90210");
        result.Should().NotContain(d => !d.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_WithZipFilter_ExcludesDealsFromOtherZips()
    {
        // Arrange
        var db = CreateInMemoryDb();
        db.Deals.AddRange(
            MakeDeal(isActive: true, zip: "10001"),
            MakeDeal(isActive: true, zip: "10002"),
            MakeDeal(isActive: true, zip: "10003")
        );
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync(zip: "10001");

        // Assert
        result.Should().HaveCount(1);
        result[0].LocationZip.Should().Be("10001");
    }

    [Fact]
    public async Task ExecuteAsync_NoZipFilter_ReturnsAllActiveDeals()
    {
        // Arrange
        var db = CreateInMemoryDb();
        db.Deals.AddRange(
            MakeDeal(isActive: true, zip: "10001"),
            MakeDeal(isActive: true, zip: null),
            MakeDeal(isActive: false, zip: "10001")
        );
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act
        var result = await sut.ExecuteAsync();

        // Assert
        result.Should().HaveCount(2);
    }
}
