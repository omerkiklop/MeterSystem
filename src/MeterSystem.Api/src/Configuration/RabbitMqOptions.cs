using System.ComponentModel.DataAnnotations;

namespace MeterSystem.Api.Configuration;

public sealed class RabbitMqOptions
{
    [Required]
    public required string Host { get; init; }

    public int Port { get; init; } = 5672;

    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    public required string QueueName { get; init; }
}
