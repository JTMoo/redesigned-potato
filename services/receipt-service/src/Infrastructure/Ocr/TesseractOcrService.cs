using Microsoft.Extensions.Logging;

namespace ReceiptService.Infrastructure.Ocr;

public sealed class TesseractOcrService : IOcrService
{
    private readonly ILogger<TesseractOcrService> _logger;

    public TesseractOcrService(ILogger<TesseractOcrService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task<IReadOnlyList<ExtractedItem>> ExtractItemsAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        _logger.LogInformation("OCR stub invoked — returning hardcoded items");

        // OCR stub: always returns the same 3 hardcoded items for MVP
        IReadOnlyList<ExtractedItem> items = new List<ExtractedItem>
        {
            new("Milk 1L", 1, 1.29m),
            new("Bread", 2, 0.89m),
            new("Orange Juice", 1, 2.49m),
        };

        return Task.FromResult(items);
    }
}
