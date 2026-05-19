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

    private static Deal MakeDeal(bool isActive, string? zip = null, DateTimeOffset? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Deal",
        Description = "Desc",
        DiscountAmount = 5m,
        LocationZip = zip,
        IsActive = isActive,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
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
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(d => d.IsActive.Should().BeTrue());
        result.TotalCount.Should().Be(2);
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
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().NotContain(d => d.LocationZip == "90210");
        result.Items.Should().NotContain(d => !d.IsActive);
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
        result.Items.Should().HaveCount(1);
        result.Items[0].LocationZip.Should().Be("10001");
        result.TotalCount.Should().Be(1);
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
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_Page2_ReturnsSecondPageItems()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            db.Deals.Add(MakeDeal(isActive: true, createdAt: baseTime.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act — page 1 has 3 items, page 2 has 2 items
        var page1 = await sut.ExecuteAsync(page: 1, pageSize: 3);
        var page2 = await sut.ExecuteAsync(page: 2, pageSize: 3);

        // Assert
        page1.Items.Should().HaveCount(3);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(3);
        page1.TotalCount.Should().Be(5);

        page2.Items.Should().HaveCount(2);
        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(3);
        page2.TotalCount.Should().Be(5);

        // Pages must not overlap
        page1.Items.Select(d => d.Id).Should().NotIntersectWith(page2.Items.Select(d => d.Id));
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeClamped_WhenExceedsMax()
    {
        // Arrange
        var db = CreateInMemoryDb();
        db.Deals.Add(MakeDeal(isActive: true));
        await db.SaveChangesAsync();

        var sut = new ListDealsUseCase(db, NullLogger<ListDealsUseCase>.Instance);

        // Act — request pageSize well above 100
        var result = await sut.ExecuteAsync(page: 1, pageSize: 500);

        // Assert — pageSize is clamped to 100 in the result
        result.PageSize.Should().Be(100);
    }
}
