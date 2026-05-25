using System.Data;
using Dapper;
using MediatR;
using MeterSystem.Worker.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MeterSystem.Worker.Commands;

public sealed class ProcessReadingsCommandHandler(
    IOptions<DatabaseOptions> dbOptions,
    ILogger<ProcessReadingsCommandHandler> logger)
    : IRequestHandler<ProcessReadingsCommand>
{
    private readonly DatabaseOptions _db = dbOptions.Value;

    public async Task Handle(ProcessReadingsCommand command, CancellationToken ct)
    {
        var message = command.Message;

        await using var db = new NpgsqlConnection(_db.ConnectionString);
        await db.OpenAsync(ct);

        var meterId = await GetOrCreateMeterAsync(db, message.MeterNumber, ct);

        foreach (var (valueAt, value) in message.Readings)
            await InsertReadingAsync(db, meterId, valueAt, value, ct);

        logger.LogInformation("Persisted {Count} reading(s) for meter {Meter}", message.Readings.Count, message.MeterNumber);
    }

    private static async Task<long> GetOrCreateMeterAsync(IDbConnection db, long meterNumber, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO meters (meter_number) VALUES (@MeterNumber)
            ON CONFLICT (meter_number) DO NOTHING;
            SELECT meter_id FROM meters WHERE meter_number = @MeterNumber;
            """;

        return await db.QuerySingleAsync<long>(
            new CommandDefinition(sql, new { MeterNumber = meterNumber }, cancellationToken: ct));
    }

    private static async Task InsertReadingAsync(
        IDbConnection db, long meterId, DateTimeOffset valueAt, double value, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO meter_readings (meter_id, value_at, value)
            VALUES (@MeterId, @ValueAt, @Value)
            ON CONFLICT (meter_id, value_at) DO NOTHING;
            """;

        await db.ExecuteAsync(
            new CommandDefinition(sql, new { MeterId = meterId, ValueAt = valueAt, Value = value }, cancellationToken: ct));
    }
}
