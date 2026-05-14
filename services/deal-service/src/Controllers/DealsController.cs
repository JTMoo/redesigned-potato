using DealService.Data;
using DealService.Domain;
using EventContracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace DealService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class DealsController : ControllerBase
{
    private readonly DealDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;

    public DealsController(DealDbContext db, IPublishEndpoint publish, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _publish = publish;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        var query = _db.Deals.AsQueryable();
        if (activeOnly) query = query.Where(d => d.IsActive);
        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var deal = await _db.Deals.FindAsync(id);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDealRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _clock.UtcNow;
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            DiscountAmount = request.DiscountAmount,
            LocationZip = request.LocationZip,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync();
        await _publish.Publish(new DealCreatedEvent(
            deal.Id, deal.Title, deal.Description, deal.DiscountAmount, deal.LocationZip, now));
        return CreatedAtAction(nameof(GetById), new { id = deal.Id }, deal);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDealRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deal = await _db.Deals.FindAsync(id);
        if (deal is null) return NotFound();
        var now = _clock.UtcNow;
        deal.Title = request.Title;
        deal.Description = request.Description;
        deal.DiscountAmount = request.DiscountAmount;
        deal.LocationZip = request.LocationZip;
        deal.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await _publish.Publish(new DealUpdatedEvent(
            deal.Id, deal.Title, deal.Description, deal.DiscountAmount, deal.LocationZip, now));
        return Ok(deal);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var deal = await _db.Deals.FindAsync(id);
        if (deal is null) return NotFound();
        deal.IsActive = false;
        deal.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync();
        await _publish.Publish(new DealArchivedEvent(deal.Id, _clock.UtcNow));
        return NoContent();
    }
}

public sealed record CreateDealRequest(
    string Title,
    string Description,
    decimal DiscountAmount,
    string? LocationZip
);

public sealed record UpdateDealRequest(
    string Title,
    string Description,
    decimal DiscountAmount,
    string? LocationZip
);
