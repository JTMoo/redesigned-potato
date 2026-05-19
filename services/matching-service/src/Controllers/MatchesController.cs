using MatchingService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchingService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class MatchesController : ControllerBase
{
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
    public async Task<IActionResult> GetForUser(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var rawUserId) ||
            !Guid.TryParse(rawUserId, out var userId))
        {
            return BadRequest(new { error = "Missing or invalid X-User-Id header." });
        }

        var matches = await _db.Matches
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.ReceiptId,
                m.DealId,
                m.EstimatedSavings,
                m.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(matches);
    }
}
