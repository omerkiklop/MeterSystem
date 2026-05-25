using Dapper;
using MeterSystem.Worker.Cache;
using MeterSystem.Worker.Commands;
using MeterSystem.Worker.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace MeterSystem.Worker.IntegrationTests.Fixtures;

public sealed class WorkerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = null!;
    public IConnectionMultiplexer RedisMultiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        RedisMultiplexer = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());

        await ApplySchemaAsync();
    }

    public async Task DisposeAsync()
    {
        RedisMultiplexer.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    public ProcessReadingsCommandHandler BuildHandler(IReadingsCache? cache = null)
    {
        var dbOptions = Options.Create(new DatabaseOptions { ConnectionString = PostgresConnectionString });
        cache ??= new RedisReadingsCache(RedisMultiplexer);
        return new ProcessReadingsCommandHandler(
            dbOptions,
            cache,
            NullLogger<ProcessReadingsCommandHandler>.Instance);
    }

    public async Task ResetAsync()
    {
        await using var db = new NpgsqlConnection(PostgresConnectionString);
        await db.ExecuteAsync("TRUNCATE meter_readings, meters RESTART IDENTITY CASCADE");
        await RedisMultiplexer.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    public async Task<IReadOnlyList<(long MeterId, DateTimeOffset ValueAt, double Value)>> GetReadingsAsync(long meterNumber)
    {
        await using var db = new NpgsqlConnection(PostgresConnectionString);
        var rows = await db.QueryAsync<(long MeterId, DateTimeOffset ValueAt, double Value)>("""
            SELECT mr.meter_id, mr.value_at, mr.value::double precision
            FROM meter_readings mr
            JOIN meters m ON m.meter_id = mr.meter_id
            WHERE m.meter_number = @MeterNumber
            ORDER BY mr.value_at
            """, new { MeterNumber = meterNumber });
        return rows.ToList();
    }

    private async Task ApplySchemaAsync()
    {
        const string ddl = """
            CREATE TABLE IF NOT EXISTS meters (
                meter_id BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL PRIMARY KEY,
                meter_number BIGINT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS meter_readings (
                meter_id BIGINT NOT NULL REFERENCES meters(meter_id),
                value_at TIMESTAMPTZ NOT NULL,
                value NUMERIC NOT NULL,
                received_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                PRIMARY KEY (meter_id, value_at)
            );
            """;

        await using var db = new NpgsqlConnection(PostgresConnectionString);
        await db.ExecuteAsync(ddl);
    }
}
