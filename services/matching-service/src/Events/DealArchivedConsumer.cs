using EventContracts.Events;
using MassTransit;
using MatchingService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class DealArchivedConsumer : IConsumer<DealArchivedEvent>
{
    private readonly MatchingDbContext _db;
    private readonly ILogger<DealArchivedConsumer> _logger;

    public DealArchivedConsumer(MatchingDbContext db, ILogger<DealArchivedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DealArchivedEvent> context)
    {
        var dealId = context.Message.DealId;

        var cached = await _db.RecommendationCache
            .FirstOrDefaultAsync(r => r.DealId == dealId, context.CancellationToken);

        if (cached is null)
        {
            _logger.LogWarning("Deal {DealId} not found in cache — nothing to remove", dealId);
            return;
        }

        _db.RecommendationCache.Remove(cached);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Deal {DealId} removed from recommendation cache", dealId);
    }
}
