using System.Text.Json;
using MediatR;
using MeterSystem.Shared.Messages;
using MeterSystem.Worker.Commands;
using MeterSystem.Worker.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MeterSystem.Worker.Consumers;

public sealed class MeterReadingsConsumer(
    IOptions<RabbitMqOptions> rabbitOptions,
    ISender sender,
    ILogger<MeterReadingsConsumer> logger)
    : BackgroundService
{
    private readonly RabbitMqOptions _rabbit = rabbitOptions.Value;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilCancelledAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ connection lost. Reconnecting in {Delay}s", ReconnectDelay.TotalSeconds);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeUntilCancelledAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Host,
            Port = _rabbit.Port,
            UserName = _rabbit.Username,
            Password = _rabbit.Password,
            DispatchConsumersAsync = true
        };

        logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", _rabbit.Host, _rabbit.Port);

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(_rabbit.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += (_, ea) => HandleMessageAsync(ea, channel, ct);
        channel.BasicConsume(_rabbit.QueueName, autoAck: false, consumer: consumer);

        logger.LogInformation("Connected. Consuming queue '{Queue}'", _rabbit.QueueName);

        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, IModel channel, CancellationToken ct)
    {
        MeterReadingMessage? message = null;
        try
        {
            message = JsonSerializer.Deserialize<MeterReadingMessage>(ea.Body.ToArray());
            if (message is null)
                throw new InvalidOperationException("Deserialized message is null");

            await sender.Send(new ProcessReadingsCommand(message), ct);

            channel.BasicAck(ea.DeliveryTag, multiple: false);
            logger.LogInformation("Processed {Count} reading(s) for meter {Meter}", message.Readings.Count, message.MeterNumber);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize message — discarding (dead-letter)");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process meter {Meter} — requeuing", message?.MeterNumber);
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }
}
