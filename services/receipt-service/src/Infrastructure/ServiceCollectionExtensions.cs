using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptService.Data;
using ReceiptService.Features.Ocr;
using ReceiptService.Features.Storage;
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

        services.Configure<MinioStorageOptions>(opts =>
        {
            opts.Endpoint = configuration["MinIO:Endpoint"] ?? "minio:9000";
            opts.AccessKey = configuration["MinIO:AccessKey"] ?? string.Empty;
            opts.SecretKey = configuration["MinIO:SecretKey"] ?? string.Empty;
            opts.BucketName = configuration["MinIO:BucketName"] ?? "receipts";
        });
        services.AddSingleton<IStorageService, MinioStorageService>();
        services.AddTransient<TesseractOcrService>();
        services.AddSingleton<IOcrServiceFactory, OcrServiceFactory>();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration["RabbitMq:Host"] ?? "rabbitmq", "/", h =>
                {
                    h.Username(configuration["RabbitMq:Username"] ?? "guest");
                    h.Password(configuration["RabbitMq:Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
