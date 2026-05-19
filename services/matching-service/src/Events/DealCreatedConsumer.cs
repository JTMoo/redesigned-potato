using EventContracts.Events;
using MassTransit;
using MatchingService.Data;
using MatchingService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Utilities;

namespace MatchingService.Events;

public sealed class DealCreatedConsumer : IConsumer<DealCreatedEvent>
{
    private readonly MatchingDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DealCreatedConsumer> _logger;

    public DealCreatedConsumer(
        MatchingDbContext db,
        IDateTimeProvider clock,
        ILogger<DealCreatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DealCreatedEvent> context)
    {
        var msg = context.Message;

        var existing = await _db.RecommendationCache
            .FirstOrDefaultAsync(r => r.DealId == msg.DealId, context.CancellationToken);

        if (existing is not null)
        {
            _logger.LogWarning("Deal {DealId} already in cache — skipping duplicate DealCreatedEvent", msg.DealId);
            return;
        }

        _db.RecommendationCache.Add(new RecommendationCache
        {
            Id = Guid.NewGuid(),
            DealId = msg.DealId,
            Title = msg.Title,
            Description = msg.Description,
            DiscountAmount = msg.DiscountAmount,
            LocationZip = msg.LocationZip,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Deal {DealId} '{Title}' added to recommendation cache", msg.DealId, msg.Title);
    }
}
