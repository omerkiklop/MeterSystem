namespace MeterSystem.Worker.Cache;

public interface IReadingsCache
{
    Task<long?> GetMeterIdAsync(long meterNumber, CancellationToken ct);
    Task SetMeterIdAsync(long meterNumber, long meterId, CancellationToken ct);
    Task<bool> IsReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct);
    Task MarkReadingProcessedAsync(long meterNumber, DateTimeOffset valueAt, CancellationToken ct);
}
