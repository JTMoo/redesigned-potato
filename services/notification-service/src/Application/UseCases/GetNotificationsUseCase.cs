using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Data;
using NotificationService.Domain;

namespace NotificationService.Application.UseCases;

public sealed class GetNotificationsUseCase
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<GetNotificationsUseCase> _logger;

    public GetNotificationsUseCase(NotificationDbContext db, ILogger<GetNotificationsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationLog>> ExecuteAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        _logger.LogInformation("Fetching notifications for user {UserId}", userId);

        return await _db.Logs
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.IsRead)         // false (unread) sorts before true (read)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
