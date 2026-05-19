using DealService.Data;
using DealService.Domain;
using EventContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Utilities;

namespace DealService.Application.UseCases;

public sealed class CreateDealUseCase
{
    private readonly DealDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreateDealUseCase> _logger;

    public CreateDealUseCase(
        DealDbContext db,
        IPublishEndpoint publish,
        IDateTimeProvider clock,
        ILogger<CreateDealUseCase> logger)
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

    public async Task<Deal> ExecuteAsync(
        string title,
        string description,
        decimal discountAmount,
        string? locationZip,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        var now = _clock.UtcNow;
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            DiscountAmount = discountAmount,
            LocationZip = locationZip,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deal {DealId} created with title {Title}", deal.Id, deal.Title);

        await _publish.Publish(
            new DealCreatedEvent(deal.Id, deal.Title, deal.Description, deal.DiscountAmount, deal.LocationZip, now),
            cancellationToken);

        return deal;
    }
}
