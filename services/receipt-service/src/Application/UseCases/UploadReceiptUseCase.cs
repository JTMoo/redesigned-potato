using EventContracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReceiptService.Application.DTOs;
using ReceiptService.Data;
using ReceiptService.Domain;
using ReceiptService.Infrastructure.Ocr;
using ReceiptService.Infrastructure.Storage;
using Utilities;

namespace ReceiptService.Application.UseCases;

public sealed class UploadReceiptUseCase
{
    private readonly ReceiptDbContext _db;
    private readonly IReceiptStorage _storage;
    private readonly IOcrService _ocrService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UploadReceiptUseCase> _logger;

    public UploadReceiptUseCase(
        ReceiptDbContext db,
        IReceiptStorage storage,
        IOcrService ocrService,
        IPublishEndpoint publishEndpoint,
        IDateTimeProvider clock,
        ILogger<UploadReceiptUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(ocrService);
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _storage = storage;
        _ocrService = ocrService;
        _publishEndpoint = publishEndpoint;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReceiptDto> ExecuteAsync(
        Guid userId,
        Stream imageStream,
        string fileName,
        string contentType,
        string? storeName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentType);

        var receiptId = Guid.NewGuid();
        var now = _clock.UtcNow;

        // Persist receipt in Pending state first
        var receipt = new Receipt
        {
            Id = receiptId,
            UserId = userId,
            StoreName = storeName ?? string.Empty,
            TotalAmount = 0m,
            Status = ReceiptStatus.Pending,
            CreatedAt = now,
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Receipt {ReceiptId} created for user {UserId} in Pending state",
            receiptId, userId);

        // Upload file to MinIO
        var imagePath = await _storage.UploadAsync(
            userId, receiptId, fileName, imageStream, contentType, cancellationToken);

        receipt.ImagePath = imagePath;
        receipt.Status = ReceiptStatus.Processing;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Receipt {ReceiptId} file uploaded to {ImagePath}, status -> Processing",
            receiptId, imagePath);

        // Publish ReceiptCreatedEvent immediately after persisting
        await _publishEndpoint.Publish(
            new ReceiptCreatedEvent(receiptId, userId, receipt.StoreName, receipt.TotalAmount, now),
            cancellationToken);

        // Run OCR
        IReadOnlyList<Infrastructure.Ocr.ExtractedItem> ocrItems;
        try
        {
            ocrItems = await _ocrService.ExtractItemsAsync(imageStream, cancellationToken);
        }
        catch (Exception ex)
        {
            // OCR failure: receipt stays in Processing; do NOT publish ItemsExtractedEvent
            _logger.LogError(ex, "OCR failed for receipt {ReceiptId}", receiptId);
            await _db.SaveChangesAsync(cancellationToken);
            return MapToDto(receipt);
        }

        // Persist items and move to Processed
        var receiptItems = ocrItems.Select(i => new ReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Total = i.Quantity * i.UnitPrice,
        }).ToList();

        receipt.TotalAmount = receiptItems.Sum(i => i.Total);
        receipt.Status = ReceiptStatus.Processed;

        _db.ReceiptItems.AddRange(receiptItems);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Receipt {ReceiptId} processed with {ItemCount} items, total={Total}",
            receiptId, receiptItems.Count, receipt.TotalAmount);

        // Publish ItemsExtractedEvent after OCR and items saved
        var contractItems = ocrItems
            .Select(i => new global::EventContracts.Events.ExtractedItem(i.Description, i.Quantity, i.UnitPrice))
            .ToList();

        await _publishEndpoint.Publish(
            new ItemsExtractedEvent(receiptId, userId, contractItems, _clock.UtcNow),
            cancellationToken);

        return MapToDto(receipt);
    }

    private static ReceiptDto MapToDto(Receipt receipt) =>
        new(
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
