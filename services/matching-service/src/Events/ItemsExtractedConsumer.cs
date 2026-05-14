using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class ItemsExtractedConsumer : IConsumer<ItemsExtractedEvent>
{
    private readonly ILogger<ItemsExtractedConsumer> _logger;

    public ItemsExtractedConsumer(ILogger<ItemsExtractedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ItemsExtractedEvent> context)
    {
        _logger.LogInformation("Items extracted from receipt {ReceiptId} — matching stub returns no matches",
            context.Message.ReceiptId);
        return Task.CompletedTask;
    }
}
