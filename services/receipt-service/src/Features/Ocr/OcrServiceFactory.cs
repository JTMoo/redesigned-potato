namespace ReceiptService.Features.Ocr;

public sealed class OcrServiceFactory : IOcrServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OcrServiceFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    public IOcrService Create() =>
        _serviceProvider.GetRequiredService<TesseractOcrService>();
}
