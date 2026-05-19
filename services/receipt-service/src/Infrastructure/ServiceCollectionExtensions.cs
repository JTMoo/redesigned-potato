using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minio;
using ReceiptService.Application.UseCases;
using ReceiptService.Data;
using ReceiptService.Infrastructure.Ocr;
using ReceiptService.Infrastructure.Storage;
using Utilities;

namespace ReceiptService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReceiptServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ReceiptDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // MinIO
        var endpoint = configuration["MinIO__Endpoint"] ?? "http://minio:9000";
        var accessKey = configuration["MinIO__AccessKey"] ?? "minioadmin";
        var secretKey = configuration["MinIO__SecretKey"] ?? "minioadmin";
        var bucketName = configuration["MinIO__BucketName"] ?? "receipts";

        // Strip protocol for Minio SDK (it expects host[:port] only)
        var endpointHost = endpoint.Replace("http://", "").Replace("https://", "");
        var useSsl = endpoint.StartsWith("https://");

        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(endpointHost)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSsl)
                .Build());

        services.AddSingleton<IReceiptStorage>(sp =>
            new MinioReceiptStorage(
                sp.GetRequiredService<IMinioClient>(),
                bucketName,
                sp.GetRequiredService<ILogger<MinioReceiptStorage>>()));

        // OCR
        services.AddTransient<IOcrService, TesseractOcrService>();

        // Use cases (scoped — they depend on DbContext which is scoped)
        services.AddScoped<UploadReceiptUseCase>();
        services.AddScoped<GetReceiptsUseCase>();
        services.AddScoped<GetReceiptUseCase>();

        // MassTransit / RabbitMQ
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ__Host"] ?? "rabbitmq", "/", h =>
                {
                    h.Username(configuration["RabbitMQ__User"] ?? "guest");
                    h.Password(configuration["RabbitMQ__Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    /// <summary>
    /// Ensures the MinIO bucket exists. Called once at application startup.
    /// </summary>
    public static async Task EnsureMinioReadyAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var storage = services.GetRequiredService<IReceiptStorage>();
        await storage.EnsureBucketExistsAsync();
    }
}
