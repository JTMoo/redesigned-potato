using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Events;
using Utilities;

namespace NotificationService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<NotificationDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserCreatedConsumer>();
            x.AddConsumer<PotentialSavingsFoundConsumer>();

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
