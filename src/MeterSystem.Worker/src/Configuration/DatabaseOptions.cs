using System.ComponentModel.DataAnnotations;

namespace MeterSystem.Worker.Configuration;

public sealed class DatabaseOptions
{
    [Required]
    public required string ConnectionString { get; init; }
}
