using MeterSystem.Worker.Cache;
using MeterSystem.Worker.Commands;
using MeterSystem.Worker.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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

    public static IServiceCollection AddCache(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<RedisOptions>()
            .BindConfiguration("Redis")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(opts.ConnectionString);
        });

        services.AddSingleton<IReadingsCache, RedisReadingsCache>();
        return services;
    }
}
