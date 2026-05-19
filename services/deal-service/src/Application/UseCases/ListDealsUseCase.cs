using DealService.Data;
using DealService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DealService.Application.UseCases;

public sealed class ListDealsUseCase
{
    private readonly DealDbContext _db;
    private readonly ILogger<ListDealsUseCase> _logger;

    public ListDealsUseCase(DealDbContext db, ILogger<ListDealsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Deal>> ExecuteAsync(
        string? zip = null,
        CancellationToken cancellationToken = default)
    {
        // Always returns active deals only; optionally filtered by zip
        var query = _db.Deals.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(zip))
        {
            // Match deals that have no zip (applies broadly) or have the specified zip
            query = query.Where(d => d.LocationZip == null || d.LocationZip == zip);
        }

        var deals = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);

        _logger.LogInformation("Listed {Count} active deals (zip filter: {Zip})", deals.Count, zip ?? "none");

        return deals;
    }
}
