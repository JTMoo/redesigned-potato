using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class DealUpdatedConsumer : IConsumer<DealUpdatedEvent>
{
    private readonly ILogger<DealUpdatedConsumer> _logger;

    public DealUpdatedConsumer(ILogger<DealUpdatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DealUpdatedEvent> context)
    {
        _logger.LogInformation("Deal {DealId} updated — cache invalidated (stub)", context.Message.DealId);
        return Task.CompletedTask;
    }
}
