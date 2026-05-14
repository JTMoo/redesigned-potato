using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MatchingService.Events;

public sealed class DealArchivedConsumer : IConsumer<DealArchivedEvent>
{
    private readonly ILogger<DealArchivedConsumer> _logger;

    public DealArchivedConsumer(ILogger<DealArchivedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DealArchivedEvent> context)
    {
        _logger.LogInformation("Deal {DealId} archived — removed from cache (stub)", context.Message.DealId);
        return Task.CompletedTask;
    }
}
