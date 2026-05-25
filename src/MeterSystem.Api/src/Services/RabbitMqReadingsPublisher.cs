using System.Text.Json;
using MeterSystem.Api.Configuration;
using MeterSystem.Shared.Messages;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MeterSystem.Api.Services;

public sealed class RabbitMqReadingsPublisher(
    IConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqReadingsPublisher> logger)
    : IReadingsPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public Task PublishAsync(MeterReadingMessage message, CancellationToken ct)
    {
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var props = channel.CreateBasicProperties();
        props.Persistent = true;

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        channel.BasicPublish(exchange: "", routingKey: _options.QueueName, basicProperties: props, body: body);

        logger.LogInformation(
            "Published {Count} reading(s) for meter {MeterNumber} to queue '{Queue}' ({Bytes} bytes)",
            message.Readings.Count, message.MeterNumber, _options.QueueName, body.Length);

        return Task.CompletedTask;
    }
}
