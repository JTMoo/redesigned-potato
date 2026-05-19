using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Logging;

namespace ReceiptService.Infrastructure.Storage;

public sealed class MinioReceiptStorage : IReceiptStorage
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly ILogger<MinioReceiptStorage> _logger;

    public MinioReceiptStorage(
        IMinioClient minioClient,
        string bucketName,
        ILogger<MinioReceiptStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(minioClient);
        ArgumentNullException.ThrowIfNull(bucketName);
        ArgumentNullException.ThrowIfNull(logger);
        _minioClient = minioClient;
        _bucketName = bucketName;
        _logger = logger;
    }

    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        var exists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName), cancellationToken);

        if (!exists)
        {
            _logger.LogInformation("Creating MinIO bucket {BucketName}", _bucketName);
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName), cancellationToken);
        }
    }

    public async Task<string> UploadAsync(
        Guid userId,
        Guid receiptId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);

        var objectKey = $"{userId}/{receiptId}/{fileName}";

        _logger.LogInformation(
            "Uploading receipt file to MinIO: bucket={Bucket}, key={Key}",
            _bucketName, objectKey);

        await _minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType),
            cancellationToken);

        return objectKey;
    }
}
