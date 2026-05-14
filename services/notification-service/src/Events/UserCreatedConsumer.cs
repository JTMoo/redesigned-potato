using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Data;
using NotificationService.Domain;
using Utilities;

namespace NotificationService.Events;

public sealed class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly NotificationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(NotificationDbContext db, IDateTimeProvider clock, ILogger<UserCreatedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = context.Message.UserId,
            Channel = "email",
            IsActive = true,
            CreatedAt = _clock.UtcNow,
        };
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created email subscription for user {UserId}", context.Message.UserId);
    }
}
