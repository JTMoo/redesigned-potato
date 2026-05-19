using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReceiptService.Application.DTOs;
using ReceiptService.Data;

namespace ReceiptService.Application.UseCases;

public sealed class GetReceiptsUseCase
{
    private readonly ReceiptDbContext _db;
    private readonly ILogger<GetReceiptsUseCase> _logger;

    public GetReceiptsUseCase(ReceiptDbContext db, ILogger<GetReceiptsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReceiptDto>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing receipts for user {UserId}", userId);

        var receipts = await _db.Receipts
            .Include(r => r.Items)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return receipts.Select(r => new ReceiptDto(
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
    }
}
