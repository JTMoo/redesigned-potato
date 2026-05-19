using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReceiptService.Application.DTOs;
using ReceiptService.Data;

namespace ReceiptService.Application.UseCases;

public sealed class GetReceiptUseCase
{
    private readonly ReceiptDbContext _db;
    private readonly ILogger<GetReceiptUseCase> _logger;

    public GetReceiptUseCase(ReceiptDbContext db, ILogger<GetReceiptUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the receipt if it exists and belongs to the requesting user.
    /// Returns null when not found or owned by a different user (treat as 404 to avoid info leakage).
    /// </summary>
    public async Task<ReceiptDto?> ExecuteAsync(
        Guid receiptId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching receipt {ReceiptId} for user {UserId}", receiptId, requestingUserId);

        var receipt = await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt is null || receipt.UserId != requestingUserId)
            return null;

        return new ReceiptDto(
            receipt.Id,
            receipt.UserId,
            receipt.StoreName,
            receipt.TotalAmount,
            receipt.ImagePath,
            receipt.Status.ToString(),
            receipt.CreatedAt,
            receipt.Items.Select(i => new ReceiptItemDto(
                i.Id, i.Description, i.Quantity, i.UnitPrice, i.Total)).ToList()
        );
    }
}
