using DealService.Application.UseCases;
using DealService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Utilities;

namespace DealService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDealServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<DealDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<CreateDealUseCase>();
        services.AddScoped<ListDealsUseCase>();
        services.AddScoped<UpdateDealUseCase>();
        services.AddScoped<ArchiveDealUseCase>();

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
}
