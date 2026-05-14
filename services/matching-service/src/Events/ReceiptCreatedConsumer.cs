using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class ReceiptCreatedConsumer : IConsumer<ReceiptCreatedEvent>
{
    private readonly ILogger<ReceiptCreatedConsumer> _logger;

    public ReceiptCreatedConsumer(ILogger<ReceiptCreatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ReceiptCreatedEvent> context)
    {
        _logger.LogInformation("Receipt {ReceiptId} created for user {UserId} — no matches found (stub)",
            context.Message.ReceiptId, context.Message.UserId);
        return Task.CompletedTask;
    }
}
