using MassTransit;
using MatchingService.Data;
using MatchingService.Events;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace MatchingService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMatchingServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<MatchingDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<ReceiptCreatedConsumer>();
            x.AddConsumer<ItemsExtractedConsumer>();
            x.AddConsumer<DealCreatedConsumer>();
            x.AddConsumer<DealUpdatedConsumer>();
            x.AddConsumer<DealArchivedConsumer>();

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
}
