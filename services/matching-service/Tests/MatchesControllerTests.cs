using FluentAssertions;
using MatchingService.Controllers;
using Xunit;
using MatchingService.Data;
using MatchingService.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

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

    [Fact]
    public async Task GetForUser_WithMissingHeader_ReturnsBadRequest()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        // Act
        var result = await _sut.GetForUser(CancellationToken.None);

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

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = userId.ToString();
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await _sut.GetForUser(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var list = ok.Value as IEnumerable<object>;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetForUser_WhenNoMatchesExist_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = userId.ToString();
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await _sut.GetForUser(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var list = ok.Value as IEnumerable<object>;
        list.Should().BeEmpty();
    }
}
