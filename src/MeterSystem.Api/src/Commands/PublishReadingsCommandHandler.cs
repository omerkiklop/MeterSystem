using MediatR;
using MeterSystem.Api.Services;

namespace MeterSystem.Api.Commands;

public sealed class PublishReadingsCommandHandler(IReadingsPublisher publisher)
    : IRequestHandler<PublishReadingsCommand>
{
    public Task Handle(PublishReadingsCommand command, CancellationToken ct)
        => publisher.PublishAsync(command.Message, ct);
}
