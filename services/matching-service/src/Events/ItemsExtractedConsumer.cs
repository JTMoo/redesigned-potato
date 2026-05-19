using EventContracts.Events;
using MassTransit;
using MatchingService.Features;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class ItemsExtractedConsumer : IConsumer<ItemsExtractedEvent>
{
    private readonly MatchingEngine _matchingEngine;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ItemsExtractedConsumer> _logger;

    public ItemsExtractedConsumer(
        MatchingEngine matchingEngine,
        IPublishEndpoint publishEndpoint,
        ILogger<ItemsExtractedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(matchingEngine);
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(logger);
        _matchingEngine = matchingEngine;
        _publishEndpoint = publishEndpoint;
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

        // Group matches by receipt to build a single summary event per receipt
        var totalSavings = matches.Sum(m => m.EstimatedSavings);
        var storeName = msg.Items.FirstOrDefault()?.Description ?? string.Empty;

        await _publishEndpoint.Publish(
            new PotentialSavingsFoundEvent(
                UserId: msg.UserId,
                ReceiptId: msg.ReceiptId,
                StoreName: storeName,
                MatchCount: matches.Count,
                TotalSavings: totalSavings),
            context.CancellationToken);
    }
}
