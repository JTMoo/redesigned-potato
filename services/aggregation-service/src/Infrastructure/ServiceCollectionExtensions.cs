using AggregationService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace AggregationService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAggregationServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<AggregationDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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
