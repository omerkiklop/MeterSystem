using MeterSystem.Worker.Cache;

namespace MeterSystem.Worker.IntegrationTests.Fakes;

/// <summary>
/// Simulates Redis being unavailable: every call throws so the handler falls back to DB.
/// </summary>
public sealed class AlwaysMissCache : IReadingsCache
{
    public Task<long?> GetMeterIdAsync(long meterNumber, CancellationToken ct) =>
        throw new Exception("Redis unavailable");

    public Task SetMeterIdAsync(long meterNumber, long meterId, CancellationToken ct) =>
        throw new Exception("Redis unavailable");

    public Task<bool> IsReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct) =>
        throw new Exception("Redis unavailable");

    public Task MarkReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct) =>
        throw new Exception("Redis unavailable");
}
