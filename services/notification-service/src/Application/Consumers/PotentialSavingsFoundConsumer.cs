using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Data;
using NotificationService.Domain;
using Utilities;

namespace NotificationService.Application.Consumers;

public sealed class PotentialSavingsFoundConsumer : IConsumer<PotentialSavingsFoundEvent>
{
    private readonly NotificationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PotentialSavingsFoundConsumer> _logger;

    public PotentialSavingsFoundConsumer(
        NotificationDbContext db,
        IDateTimeProvider clock,
        ILogger<PotentialSavingsFoundConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PotentialSavingsFoundEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var evt = context.Message;

        var notification = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = evt.UserId.ToString(),
            ReceiptId = evt.ReceiptId,
            Message = $"We found {evt.MatchCount} deal(s) matching your receipt from {evt.StoreName}!",
            IsRead = false,
            CreatedAt = _clock.UtcNow,
        };

        _db.Logs.Add(notification);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Created notification {NotificationId} for user {UserId} with {MatchCount} deal(s) from receipt {ReceiptId}",
            notification.Id, evt.UserId, evt.MatchCount, evt.ReceiptId);
    }
}
