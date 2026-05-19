using DealService.Data;
using DealService.Domain;
using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Utilities;

namespace DealService.Application.UseCases;

public sealed class ArchiveDealUseCase
{
    private readonly DealDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ArchiveDealUseCase> _logger;

    public ArchiveDealUseCase(
        DealDbContext db,
        IPublishEndpoint publish,
        IDateTimeProvider clock,
        ILogger<ArchiveDealUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _publish = publish;
        _clock = clock;
        _logger = logger;
    }

    /// <returns>True if the deal was found and archived; false if not found.</returns>
    public async Task<bool> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deal = await _db.Deals.FindAsync([id], cancellationToken);
        if (deal is null)
        {
            _logger.LogWarning("Deal {DealId} not found for archiving", id);
            return false;
        }

        var now = _clock.UtcNow;
        deal.IsActive = false;
        deal.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deal {DealId} archived", deal.Id);

        await _publish.Publish(new DealArchivedEvent(deal.Id, now), cancellationToken);

        return true;
    }
}
