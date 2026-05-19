using EventContracts.Events;
using MassTransit;
using MatchingService.Features;
using Microsoft.Extensions.Logging;
using Utilities;

namespace MatchingService.Events;

public sealed class ItemsExtractedConsumer : IConsumer<ItemsExtractedEvent>
{
    private readonly MatchingEngine _matchingEngine;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ItemsExtractedConsumer> _logger;

    public ItemsExtractedConsumer(
        MatchingEngine matchingEngine,
        IPublishEndpoint publishEndpoint,
        IDateTimeProvider clock,
        ILogger<ItemsExtractedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(matchingEngine);
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _matchingEngine = matchingEngine;
        _publishEndpoint = publishEndpoint;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ItemsExtractedEvent> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "Running deal matching for receipt {ReceiptId} ({ItemCount} items)",
            msg.ReceiptId, msg.Items.Count);

        var matches = await _matchingEngine.MatchItemsAsync(
            msg.ReceiptId, msg.UserId, msg.Items, context.CancellationToken);

        if (matches.Count == 0)
        {
            _logger.LogInformation("No deals matched for receipt {ReceiptId}", msg.ReceiptId);
            return;
        }

        _logger.LogInformation(
            "{MatchCount} deal(s) matched for receipt {ReceiptId} — publishing savings events",
            matches.Count, msg.ReceiptId);

        var now = _clock.UtcNow;

        var publishTasks = matches.Select(m => _publishEndpoint.Publish(
            new PotentialSavingsFoundEvent(
                UserId: m.UserId,
                ReceiptId: m.ReceiptId,
                MatchedDealId: m.DealId,
                EstimatedSavings: m.EstimatedSavings,
                OccurredAt: now),
            context.CancellationToken));

        await Task.WhenAll(publishTasks);
    }
}
