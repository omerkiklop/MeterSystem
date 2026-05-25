using MediatR;
using MeterSystem.Shared.Messages;

namespace MeterSystem.Api.Commands;

public record PublishReadingsCommand(MeterReadingMessage Message) : IRequest;
