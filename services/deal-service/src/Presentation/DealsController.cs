using DealService.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace DealService.Presentation;

[ApiController]
[Route("[controller]")]
public sealed class DealsController : ControllerBase
{
    private readonly CreateDealUseCase _createDeal;
    private readonly ListDealsUseCase _listDeals;
    private readonly UpdateDealUseCase _updateDeal;
    private readonly ArchiveDealUseCase _archiveDeal;
    private readonly DealService.Data.DealDbContext _db;

    public DealsController(
        CreateDealUseCase createDeal,
        ListDealsUseCase listDeals,
        UpdateDealUseCase updateDeal,
        ArchiveDealUseCase archiveDeal,
        DealService.Data.DealDbContext db)
    {
        ArgumentNullException.ThrowIfNull(createDeal);
        ArgumentNullException.ThrowIfNull(listDeals);
        ArgumentNullException.ThrowIfNull(updateDeal);
        ArgumentNullException.ThrowIfNull(archiveDeal);
        ArgumentNullException.ThrowIfNull(db);
        _createDeal = createDeal;
        _listDeals = listDeals;
        _updateDeal = updateDeal;
        _archiveDeal = archiveDeal;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? zip = null,
        CancellationToken cancellationToken = default)
    {
        var deals = await _listDeals.ExecuteAsync(zip, cancellationToken);
        return Ok(deals);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var deal = await _db.Deals.FindAsync([id], cancellationToken);
        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDealRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deal = await _createDeal.ExecuteAsync(
            request.Title,
            request.Description,
            request.DiscountAmount,
            request.LocationZip,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = deal.Id }, deal);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDealRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deal = await _updateDeal.ExecuteAsync(
            id,
            request.Title,
            request.Description,
            request.DiscountAmount,
            request.LocationZip,
            cancellationToken);

        return deal is null ? NotFound() : Ok(deal);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var found = await _archiveDeal.ExecuteAsync(id, cancellationToken);
        return found ? NoContent() : NotFound();
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
