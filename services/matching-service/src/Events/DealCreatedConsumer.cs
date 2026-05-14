using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class DealCreatedConsumer : IConsumer<DealCreatedEvent>
{
    private readonly ILogger<DealCreatedConsumer> _logger;

    public DealCreatedConsumer(ILogger<DealCreatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DealCreatedEvent> context)
    {
        _logger.LogInformation("Deal {DealId} created — cached for matching (stub)", context.Message.DealId);
        return Task.CompletedTask;
    }
}
