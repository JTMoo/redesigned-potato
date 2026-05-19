using FluentAssertions;
using MatchingService.Application.DTOs;
using MatchingService.Controllers;
using Xunit;
using MatchingService.Data;
using MatchingService.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchingService.Tests;

public sealed class MatchesControllerTests : IDisposable
{
    private readonly MatchingDbContext _db;
    private readonly MatchesController _sut;

    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero);

    public MatchesControllerTests()
    {
        var options = new DbContextOptionsBuilder<MatchingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MatchingDbContext(options);
        _sut = new MatchesController(_db);
    }

    public void Dispose() => _db.Dispose();

    private void SetUserId(Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = userId.ToString();
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetForUser_WithMissingHeader_ReturnsBadRequest()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        // Act
        var result = await _sut.GetForUser(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetForUser_WithValidHeader_ReturnsMatchesForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _db.Matches.AddRange(
            new PurchaseDealMatch
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReceiptId = Guid.NewGuid(),
                DealId = Guid.NewGuid(),
                EstimatedSavings = 3.00m,
                CreatedAt = FixedNow,
            },
            new PurchaseDealMatch
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                ReceiptId = Guid.NewGuid(),
                DealId = Guid.NewGuid(),
                EstimatedSavings = 1.50m,
                CreatedAt = FixedNow,
            });
        await _db.SaveChangesAsync();

        SetUserId(userId);

        // Act
        var result = await _sut.GetForUser(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var paged = ok.Value as PagedResult<object>;
        paged!.Items.Should().HaveCount(1);
        paged.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetForUser_WhenNoMatchesExist_ReturnsEmptyPagedResult()
    {
        // Arrange
        SetUserId(Guid.NewGuid());

        // Act
        var result = await _sut.GetForUser(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var paged = ok.Value as PagedResult<object>;
        paged!.Items.Should().BeEmpty();
        paged.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetForUser_Page2_ReturnsSecondPageItems()
    {
        // Arrange
        var userId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            _db.Matches.Add(new PurchaseDealMatch
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReceiptId = Guid.NewGuid(),
                DealId = Guid.NewGuid(),
                EstimatedSavings = 1.00m,
                CreatedAt = FixedNow.AddMinutes(-i),
            });
        }
        await _db.SaveChangesAsync();

        SetUserId(userId);

        // Act
        var page1Result = await _sut.GetForUser(page: 1, pageSize: 3, CancellationToken.None);
        var page2Result = await _sut.GetForUser(page: 2, pageSize: 3, CancellationToken.None);

        // Assert
        var page1 = ((OkObjectResult)page1Result).Value as PagedResult<object>;
        var page2 = ((OkObjectResult)page2Result).Value as PagedResult<object>;

        page1!.Items.Should().HaveCount(3);
        page1.TotalCount.Should().Be(5);
        page1.Page.Should().Be(1);

        page2!.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(5);
        page2.Page.Should().Be(2);
    }

    [Fact]
    public async Task GetForUser_PageSizeClamped_WhenExceedsMax()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _db.Matches.Add(new PurchaseDealMatch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReceiptId = Guid.NewGuid(),
            DealId = Guid.NewGuid(),
            EstimatedSavings = 1.00m,
            CreatedAt = FixedNow,
        });
        await _db.SaveChangesAsync();
        SetUserId(userId);

        // Act
        var result = await _sut.GetForUser(page: 1, pageSize: 500, CancellationToken.None);

        // Assert
        var paged = ((OkObjectResult)result).Value as PagedResult<object>;
        paged!.PageSize.Should().Be(100);
    }
}
