using MeterSystem.Shared.Messages;

namespace MeterSystem.Api.Services;

public interface IReadingsPublisher
{
    Task PublishAsync(MeterReadingMessage message, CancellationToken ct);
}
