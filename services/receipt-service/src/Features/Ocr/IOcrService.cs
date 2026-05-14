namespace ReceiptService.Features.Ocr;

public interface IOcrService
{
    Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default);
}

public sealed record OcrResult(
    string StoreName,
    decimal TotalAmount,
    IReadOnlyList<OcrLineItem> Items
);

public sealed record OcrLineItem(string Description, decimal Quantity, decimal UnitPrice);
