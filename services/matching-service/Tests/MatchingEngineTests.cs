using EventContracts.Events;
using FluentAssertions;
using Xunit;
using MatchingService.Data;
using MatchingService.Domain;
using MatchingService.Features;
using Microsoft.EntityFrameworkCore;
using Moq;
using Utilities;

namespace MatchingService.Tests;

public sealed class MatchingEngineTests : IDisposable
{
    private readonly MatchingDbContext _db;
    private readonly Mock<IDateTimeProvider> _clock;
    private readonly MatchingEngine _sut;

    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero);

    public MatchingEngineTests()
    {
        var options = new DbContextOptionsBuilder<MatchingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MatchingDbContext(options);
        _clock = new Mock<IDateTimeProvider>();
        _clock.SetupGet(c => c.UtcNow).Returns(FixedNow);
        _sut = new MatchingEngine(_db, _clock.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task MatchItemsAsync_WhenNoDealsInCache_ReturnsEmptyList()
    {
        // Arrange
        var items = new List<ExtractedItem>
        {
            new("Coffee beans 500g", 1, 9.99m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(Guid.NewGuid(), Guid.NewGuid(), items);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MatchItemsAsync_WhenDealTitleMatchesItemDescription_ReturnsSingleMatch()
    {
        // Arrange
        var dealId = Guid.NewGuid();
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = dealId,
            Title = "Coffee beans",
            Description = "Premium roast",
            DiscountAmount = 2.50m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var receiptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var items = new List<ExtractedItem>
        {
            new("Coffee beans 500g", 1, 9.99m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(receiptId, userId, items);

        // Assert
        result.Should().HaveCount(1);
        result[0].DealId.Should().Be(dealId);
        result[0].ReceiptId.Should().Be(receiptId);
        result[0].UserId.Should().Be(userId);
        result[0].EstimatedSavings.Should().Be(2.50m);
        result[0].CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task MatchItemsAsync_WhenItemDescriptionContainsDealDescription_ReturnsSingleMatch()
    {
        // Arrange — item description "Organic milk 1L" contains deal description keyword "Organic milk"
        var dealId = Guid.NewGuid();
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = dealId,
            Title = "Grocery deal",
            Description = "Organic milk",
            DiscountAmount = 1.00m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var items = new List<ExtractedItem>
        {
            new("Organic milk 1L", 2, 3.49m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(Guid.NewGuid(), Guid.NewGuid(), items);

        // Assert
        result.Should().HaveCount(1);
        result[0].DealId.Should().Be(dealId);
    }

    [Fact]
    public async Task MatchItemsAsync_MatchingIsCaseInsensitive()
    {
        // Arrange
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = Guid.NewGuid(),
            Title = "COFFEE BEANS",
            Description = string.Empty,
            DiscountAmount = 3.00m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var items = new List<ExtractedItem>
        {
            new("coffee beans arabica", 1, 12.00m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(Guid.NewGuid(), Guid.NewGuid(), items);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task MatchItemsAsync_WhenDealDoesNotMatchAnyItem_ReturnsEmptyList()
    {
        // Arrange
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = Guid.NewGuid(),
            Title = "Gym membership",
            Description = "Monthly pass",
            DiscountAmount = 5.00m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var items = new List<ExtractedItem>
        {
            new("Coffee beans 500g", 1, 9.99m),
            new("Milk 1L", 2, 3.49m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(Guid.NewGuid(), Guid.NewGuid(), items);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MatchItemsAsync_PersistsMatchesToDatabase()
    {
        // Arrange
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = Guid.NewGuid(),
            Title = "Coffee",
            Description = string.Empty,
            DiscountAmount = 1.50m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var receiptId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        await _sut.MatchItemsAsync(receiptId, userId, new List<ExtractedItem>
        {
            new("Coffee 200g", 1, 7.99m),
        });

        // Assert
        var saved = await _db.Matches.SingleAsync(m => m.ReceiptId == receiptId);
        saved.UserId.Should().Be(userId);
        saved.EstimatedSavings.Should().Be(1.50m);
    }

    [Fact]
    public async Task MatchItemsAsync_DoesNotCreateDuplicateMatchForSameReceiptAndDeal()
    {
        // Arrange
        var dealId = Guid.NewGuid();
        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = dealId,
            Title = "Coffee",
            Description = string.Empty,
            DiscountAmount = 1.50m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();

        var receiptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var items = new List<ExtractedItem> { new("Coffee 200g", 1, 7.99m) };

        // Act — run matching twice for the same receipt
        await _sut.MatchItemsAsync(receiptId, userId, items);
        var secondResult = await _sut.MatchItemsAsync(receiptId, userId, items);

        // Assert — second call returns no new matches
        secondResult.Should().BeEmpty();
        var allMatches = await _db.Matches.Where(m => m.ReceiptId == receiptId).ToListAsync();
        allMatches.Should().HaveCount(1);
    }

    [Fact]
    public async Task MatchItemsAsync_WithMultipleMatchingDeals_ReturnsAllMatches()
    {
        // Arrange
        _db.RecommendationCache.AddRange(
            new RecommendationCache
            {
                Id = Guid.NewGuid(),
                DealId = Guid.NewGuid(),
                Title = "Coffee",
                Description = string.Empty,
                DiscountAmount = 1.00m,
                CreatedAt = FixedNow,
            },
            new RecommendationCache
            {
                Id = Guid.NewGuid(),
                DealId = Guid.NewGuid(),
                Title = "Tea",
                Description = string.Empty,
                DiscountAmount = 0.50m,
                CreatedAt = FixedNow,
            });
        await _db.SaveChangesAsync();

        var items = new List<ExtractedItem>
        {
            new("Coffee blend 250g", 1, 6.99m),
            new("Green tea 100g", 1, 4.99m),
        };

        // Act
        var result = await _sut.MatchItemsAsync(Guid.NewGuid(), Guid.NewGuid(), items);

        // Assert
        result.Should().HaveCount(2);
    }
}
