using System.Data;
using Dapper;
using MediatR;
using MeterSystem.Worker.Cache;
using MeterSystem.Worker.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MeterSystem.Worker.Commands;

public sealed class ProcessReadingsCommandHandler(
    IOptions<DatabaseOptions> dbOptions,
    IReadingsCache cache,
    ILogger<ProcessReadingsCommandHandler> logger)
    : IRequestHandler<ProcessReadingsCommand>
{
    private readonly DatabaseOptions _db = dbOptions.Value;

    public async Task Handle(ProcessReadingsCommand command, CancellationToken ct)
    {
        var message = command.Message;

        await using var db = new NpgsqlConnection(_db.ConnectionString);
        await db.OpenAsync(ct);

        var meterId = await GetOrCreateMeterWithCacheAsync(db, message.MeterNumber, ct);

        foreach (var (valueAt, value) in message.Readings)
        {
            if (await IsReadingCachedAsync(message.MeterNumber, valueAt, ct))
                continue;

            await InsertReadingAsync(db, meterId, valueAt, value, ct);
            await CacheReadingAsync(message.MeterNumber, valueAt, ct);
        }
    }

    private async Task<long> GetOrCreateMeterWithCacheAsync(IDbConnection db, long meterNumber, CancellationToken ct)
    {
        try
        {
            var cached = await cache.GetMeterIdAsync(meterNumber, ct);
            if (cached is not null)
                return cached.Value;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable for meter lookup — falling back to DB");
        }

        var meterId = await GetOrCreateMeterAsync(db, meterNumber, ct);

        try { await cache.SetMeterIdAsync(meterNumber, meterId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to cache meter_id for meter {Meter}", meterNumber); }

        return meterId;
    }

    private async Task<bool> IsReadingCachedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct)
    {
        try { return await cache.IsReadingProcessedAsync(meterNumber, valueAt, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable for reading check — falling back to DB insert");
            return false;
        }
    }

    private async Task CacheReadingAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct)
    {
        try { await cache.MarkReadingProcessedAsync(meterNumber, valueAt, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to cache reading for meter {Meter}", meterNumber); }
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
