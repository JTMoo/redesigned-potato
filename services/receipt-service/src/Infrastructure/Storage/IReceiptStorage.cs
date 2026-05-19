namespace ReceiptService.Infrastructure.Storage;

public interface IReceiptStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);

    Task<string> UploadAsync(
        Guid userId,
        Guid receiptId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}
