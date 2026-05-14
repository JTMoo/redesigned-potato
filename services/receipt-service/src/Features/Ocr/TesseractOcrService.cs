namespace ReceiptService.Features.Ocr;

public sealed class TesseractOcrService : IOcrService
{
    public Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var result = new OcrResult(
            StoreName: "Unknown Store",
            TotalAmount: 0m,
            Items: []);
        return Task.FromResult(result);
    }
}
