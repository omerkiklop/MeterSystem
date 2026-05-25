using MeterSystem.Worker.Commands;
using MeterSystem.Worker.Configuration;

namespace MeterSystem.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .BindConfiguration("RabbitMq")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ProcessReadingsCommand>());
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<DatabaseOptions>()
            .BindConfiguration("Database")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
