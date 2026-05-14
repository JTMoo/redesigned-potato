using Microsoft.Extensions.Options;

namespace ReceiptService.Features.Storage;

public sealed class MinioStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}

public sealed class MinioStorageService : IStorageService
{
    private readonly MinioStorageOptions _options;

    public MinioStorageService(IOptions<MinioStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = $"{_options.BucketName}/{Guid.NewGuid()}/{fileName}";
        return Task.FromResult(path);
    }
}
