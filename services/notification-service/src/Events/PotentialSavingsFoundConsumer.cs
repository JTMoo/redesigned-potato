using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace NotificationService.Events;

public sealed class PotentialSavingsFoundConsumer : IConsumer<PotentialSavingsFoundEvent>
{
    private readonly ILogger<PotentialSavingsFoundConsumer> _logger;

    public PotentialSavingsFoundConsumer(ILogger<PotentialSavingsFoundConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PotentialSavingsFoundEvent> context)
    {
        _logger.LogInformation(
            "Potential savings of {Savings:C} found for user {UserId} (stub — notification not sent)",
            context.Message.EstimatedSavings, context.Message.UserId);
        return Task.CompletedTask;
    }
}
