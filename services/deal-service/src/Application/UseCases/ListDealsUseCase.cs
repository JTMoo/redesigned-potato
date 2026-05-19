using DealService.Application.DTOs;
using DealService.Data;
using DealService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DealService.Application.UseCases;

public sealed class ListDealsUseCase
{
    private const int MaxPageSize = 100;

    private readonly DealDbContext _db;
    private readonly ILogger<ListDealsUseCase> _logger;

    public ListDealsUseCase(DealDbContext db, ILogger<ListDealsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<Deal>> ExecuteAsync(
        string? zip = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Always returns active deals only; optionally filtered by zip
        var query = _db.Deals.AsNoTracking().Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(zip))
        {
            // Match deals that have no zip (applies broadly) or have the specified zip
            query = query.Where(d => d.LocationZip == null || d.LocationZip == zip);
        }

        var orderedQuery = query.OrderByDescending(d => d.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var deals = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count}/{Total} active deals (page {Page}/{PageSize}, zip filter: {Zip})",
            deals.Count, totalCount, page, pageSize, zip ?? "none");

        return new PagedResult<Deal>(deals, page, pageSize, totalCount);
    }
}
