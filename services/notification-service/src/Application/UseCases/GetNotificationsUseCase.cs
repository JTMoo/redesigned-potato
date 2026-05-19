using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Data;
using NotificationService.Domain;

namespace NotificationService.Application.UseCases;

public sealed class GetNotificationsUseCase
{
    private const int MaxPageSize = 100;

    private readonly NotificationDbContext _db;
    private readonly ILogger<GetNotificationsUseCase> _logger;

    public GetNotificationsUseCase(NotificationDbContext db, ILogger<GetNotificationsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationLog>> ExecuteAsync(
        string userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        _logger.LogInformation("Fetching notifications for user {UserId}", userId);

        var orderedQuery = _db.Logs
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.IsRead)         // false (unread) sorts before true (read)
            .ThenByDescending(n => n.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationLog>(items, page, pageSize, totalCount);
    }
}
