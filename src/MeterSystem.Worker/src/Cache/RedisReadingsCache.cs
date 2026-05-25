using StackExchange.Redis;

namespace MeterSystem.Worker.Cache;

public sealed class RedisReadingsCache(IConnectionMultiplexer redis) : IReadingsCache
{
    private readonly IDatabase _db = redis.GetDatabase();

    private static readonly TimeSpan MeterTtl   = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReadingTtl = TimeSpan.FromHours(24);

    public async Task<long?> GetMeterIdAsync(long meterNumber, CancellationToken ct)
    {
        var value = await _db.StringGetAsync(MeterKey(meterNumber));
        return value.HasValue ? long.Parse(value!) : null;
    }

    public Task SetMeterIdAsync(long meterNumber, long meterId, CancellationToken ct) =>
        _db.StringSetAsync(MeterKey(meterNumber), meterId, MeterTtl);

    public Task<bool> IsReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct) =>
        _db.KeyExistsAsync(ReadingKey(meterNumber, valueAt));

    public Task MarkReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct) =>
        _db.StringSetAsync(ReadingKey(meterNumber, valueAt), "1", ReadingTtl);

    private static string MeterKey(long meterNumber) =>
        $"meter:{meterNumber}";

    private static string ReadingKey(long meterNumber, DateTimeOffset valueAt) =>
        $"reading:{meterNumber}:{valueAt.ToUnixTimeMilliseconds()}";
}
