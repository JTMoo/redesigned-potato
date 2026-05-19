using DealService.Data;
using DealService.Domain;
using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Utilities;

namespace DealService.Application.UseCases;

public sealed class UpdateDealUseCase
{
    private readonly DealDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UpdateDealUseCase> _logger;

    public UpdateDealUseCase(
        DealDbContext db,
        IPublishEndpoint publish,
        IDateTimeProvider clock,
        ILogger<UpdateDealUseCase> logger)
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

    /// <returns>The updated deal, or null if not found.</returns>
    public async Task<Deal?> ExecuteAsync(
        Guid id,
        string title,
        string description,
        decimal discountAmount,
        string? locationZip,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        var deal = await _db.Deals.FindAsync([id], cancellationToken);
        if (deal is null)
        {
            _logger.LogWarning("Deal {DealId} not found for update", id);
            return null;
        }

        var now = _clock.UtcNow;
        deal.Title = title;
        deal.Description = description;
        deal.DiscountAmount = discountAmount;
        deal.LocationZip = locationZip;
        deal.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deal {DealId} updated", deal.Id);

        await _publish.Publish(
            new DealUpdatedEvent(deal.Id, deal.Title, deal.Description, deal.DiscountAmount, deal.LocationZip, now),
            cancellationToken);

        return deal;
    }
}
