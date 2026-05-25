using MeterSystem.Api.Commands;
using MeterSystem.Api.Configuration;
using MeterSystem.Api.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MeterSystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .BindConfiguration("RabbitMq")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnection>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var factory = new ConnectionFactory
            {
                HostName = opts.Host,
                Port = opts.Port,
                UserName = opts.Username,
                Password = opts.Password
            };
            return factory.CreateConnection();
        });

        services.AddScoped<IReadingsPublisher, RabbitMqReadingsPublisher>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PublishReadingsCommand>());
        return services;
    }
}
