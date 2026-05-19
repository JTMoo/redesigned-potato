using MassTransit;
using MatchingService.Data;
using MatchingService.Events;
using MatchingService.Features;
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
        services.AddScoped<MatchingEngine>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<ReceiptCreatedConsumer>();
            x.AddConsumer<ItemsExtractedConsumer>();
            x.AddConsumer<DealCreatedConsumer>();
            x.AddConsumer<DealUpdatedConsumer>();
            x.AddConsumer<DealArchivedConsumer>();

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
