namespace ReceiptService.Features.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
}
