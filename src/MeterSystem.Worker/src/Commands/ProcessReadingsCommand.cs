using MediatR;
using MeterSystem.Shared.Messages;

namespace MeterSystem.Worker.Commands;

public record ProcessReadingsCommand(MeterReadingMessage Message) : IRequest;
