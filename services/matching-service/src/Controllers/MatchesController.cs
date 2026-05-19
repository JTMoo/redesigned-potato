using MatchingService.Application.DTOs;
using MatchingService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchingService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class MatchesController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly MatchingDbContext _db;

    public MatchesController(MatchingDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Returns all purchase-deal matches for the given user.
    /// Reads the user id from the X-User-Id header forwarded by the API gateway.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetForUser(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var rawUserId) ||
            !Guid.TryParse(rawUserId, out var userId))
        {
            return BadRequest(new { error = "Missing or invalid X-User-Id header." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var orderedQuery = _db.Matches
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var matches = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.ReceiptId,
                m.DealId,
                m.EstimatedSavings,
                m.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<object>(matches, page, pageSize, totalCount);

        return Ok(result);
    }
}
