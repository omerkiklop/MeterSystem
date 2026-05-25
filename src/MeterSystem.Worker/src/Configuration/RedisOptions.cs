using System.ComponentModel.DataAnnotations;

namespace MeterSystem.Worker.Configuration;

public sealed class RedisOptions
{
    [Required]
    public required string ConnectionString { get; init; }
}
