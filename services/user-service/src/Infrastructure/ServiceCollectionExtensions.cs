using MassTransit;
using Microsoft.EntityFrameworkCore;
using UserService.Application.UseCases;
using UserService.Data;
using Utilities;

namespace UserService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserServiceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<UserDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Register use cases
        services.AddScoped<UpsertUserUseCase>();
        services.AddScoped<GetUserUseCase>();
        services.AddScoped<UpdatePreferencesUseCase>();

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
