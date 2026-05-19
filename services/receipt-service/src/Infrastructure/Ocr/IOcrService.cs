namespace ReceiptService.Infrastructure.Ocr;

public sealed record ExtractedItem(
    string Description,
    int Quantity,
    decimal UnitPrice
);

public interface IOcrService
{
    Task<IReadOnlyList<ExtractedItem>> ExtractItemsAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default);
}
