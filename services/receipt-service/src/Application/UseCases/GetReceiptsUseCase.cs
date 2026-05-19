using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReceiptService.Application.DTOs;
using ReceiptService.Data;

namespace ReceiptService.Application.UseCases;

public sealed class GetReceiptsUseCase
{
    private const int MaxPageSize = 100;

    private readonly ReceiptDbContext _db;
    private readonly ILogger<GetReceiptsUseCase> _logger;

    public GetReceiptsUseCase(ReceiptDbContext db, ILogger<GetReceiptsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<ReceiptDto>> ExecuteAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        _logger.LogInformation("Listing receipts for user {UserId}", userId);

        var orderedQuery = _db.Receipts
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var receipts = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = receipts.Select(r => new ReceiptDto(
            r.Id,
            r.UserId,
            r.StoreName,
            r.TotalAmount,
            r.ImagePath,
            r.Status.ToString(),
            r.CreatedAt,
            r.Items.Select(i => new ReceiptItemDto(
                i.Id, i.Description, i.Quantity, i.UnitPrice, i.Total)).ToList()
        )).ToList();

        return new PagedResult<ReceiptDto>(items, page, pageSize, totalCount);
    }
}
