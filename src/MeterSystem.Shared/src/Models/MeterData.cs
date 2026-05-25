namespace MeterSystem.Shared.Messages;

public record MeterReadingMessage(
    long MeterNumber,
    IReadOnlyDictionary<DateTimeOffset, double> Readings
);
