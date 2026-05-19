using EventContracts.Events;
using MassTransit;
using MatchingService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class DealUpdatedConsumer : IConsumer<DealUpdatedEvent>
{
    private readonly MatchingDbContext _db;
    private readonly ILogger<DealUpdatedConsumer> _logger;

    public DealUpdatedConsumer(MatchingDbContext db, ILogger<DealUpdatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DealUpdatedEvent> context)
    {
        var msg = context.Message;

        var cached = await _db.RecommendationCache
            .FirstOrDefaultAsync(r => r.DealId == msg.DealId, context.CancellationToken);

        if (cached is null)
        {
            _logger.LogWarning("Deal {DealId} not found in cache — cannot update", msg.DealId);
            return;
        }

        cached.Title = msg.Title;
        cached.Description = msg.Description;
        cached.DiscountAmount = msg.DiscountAmount;
        cached.LocationZip = msg.LocationZip;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Deal {DealId} '{Title}' updated in recommendation cache", msg.DealId, msg.Title);
    }
}
