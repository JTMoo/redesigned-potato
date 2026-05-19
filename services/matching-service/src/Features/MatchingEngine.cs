using EventContracts.Events;
using MatchingService.Data;
using MatchingService.Domain;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace MatchingService.Features;

public sealed class MatchingEngine
{
    private readonly MatchingDbContext _db;
    private readonly IDateTimeProvider _clock;

    public MatchingEngine(MatchingDbContext db, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Finds deals whose Title or Description contain any extracted item description
    /// (case-insensitive substring match).  Returns the persisted matches.
    /// </summary>
    public async Task<IReadOnlyList<PurchaseDealMatch>> MatchItemsAsync(
        Guid receiptId,
        Guid userId,
        IReadOnlyList<ExtractedItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var deals = await _db.RecommendationCache.ToListAsync(cancellationToken);

        var matches = new List<PurchaseDealMatch>();

        foreach (var deal in deals)
        {
            var matched = items.Any(item =>
                ContainsIgnoreCase(deal.Title, item.Description) ||
                ContainsIgnoreCase(item.Description, deal.Title) ||
                ContainsIgnoreCase(deal.Description, item.Description) ||
                ContainsIgnoreCase(item.Description, deal.Description));

            if (!matched)
                continue;

            // Avoid duplicate matches for the same receipt + deal combination.
            var alreadyExists = await _db.Matches.AnyAsync(
                m => m.ReceiptId == receiptId && m.DealId == deal.DealId,
                cancellationToken);

            if (alreadyExists)
                continue;

            var match = new PurchaseDealMatch
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReceiptId = receiptId,
                DealId = deal.DealId,
                EstimatedSavings = deal.DiscountAmount,
                CreatedAt = _clock.UtcNow,
            };

            _db.Matches.Add(match);
            matches.Add(match);
        }

        if (matches.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return matches;
    }

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
