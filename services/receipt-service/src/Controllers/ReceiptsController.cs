using EventContracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;
using ReceiptService.Domain;
using ReceiptService.Features.Ocr;
using ReceiptService.Features.Storage;
using Utilities;

namespace ReceiptService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class ReceiptsController : ControllerBase
{
    private readonly ReceiptDbContext _db;
    private readonly IOcrServiceFactory _ocrFactory;
    private readonly IStorageService _storage;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;

    public ReceiptsController(
        ReceiptDbContext db,
        IOcrServiceFactory ocrFactory,
        IStorageService storage,
        IPublishEndpoint publish,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(ocrFactory);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _ocrFactory = ocrFactory;
        _storage = storage;
        _publish = publish;
        _clock = clock;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var receipt = await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] UploadReceiptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!Guid.TryParse(userIdHeader, out var userId))
            return Unauthorized();

        var imagePath = await _storage.UploadAsync(
            request.Image.FileName,
            request.Image.OpenReadStream(),
            request.Image.ContentType);

        var ocr = _ocrFactory.Create();
        var ocrResult = await ocr.ExtractAsync(request.Image.OpenReadStream());

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StoreName = ocrResult.StoreName,
            TotalAmount = ocrResult.TotalAmount,
            ImagePath = imagePath,
            Status = ReceiptStatus.Processed,
            CreatedAt = _clock.UtcNow,
            Items = ocrResult.Items.Select(i => new ReceiptItem
            {
                Id = Guid.NewGuid(),
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Total = i.Quantity * i.UnitPrice,
            }).ToList(),
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();

        await _publish.Publish(new ReceiptCreatedEvent(
            receipt.Id, receipt.UserId, receipt.StoreName, receipt.TotalAmount, _clock.UtcNow));

        if (receipt.Items.Count > 0)
        {
            var items = receipt.Items
                .Select(i => new EventContracts.Events.ExtractedItem(i.Description, i.Quantity, i.UnitPrice))
                .ToList();
            await _publish.Publish(new ItemsExtractedEvent(receipt.Id, receipt.UserId, items, _clock.UtcNow));
        }

        return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
    }
}

public sealed record UploadReceiptRequest(IFormFile Image);
