using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Data;

namespace NotificationService.Application.UseCases;

public sealed class MarkNotificationReadUseCase
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<MarkNotificationReadUseCase> _logger;

    public MarkNotificationReadUseCase(NotificationDbContext db, ILogger<MarkNotificationReadUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <returns>True if the notification was found and updated; false if not found or not owned by the user.</returns>
    public async Task<bool> ExecuteAsync(Guid notificationId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var notification = await _db.Logs
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
        {
            _logger.LogWarning(
                "Notification {NotificationId} not found or not owned by user {UserId}",
                notificationId, userId);
            return false;
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked notification {NotificationId} as read for user {UserId}",
            notificationId, userId);

        return true;
    }
}
