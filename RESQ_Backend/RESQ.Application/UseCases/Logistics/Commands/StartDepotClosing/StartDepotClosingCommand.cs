using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.StartDepotClosing;

public record StartDepotClosingCommand(
    int DepotId,
    Guid RequestedBy
) : IRequest<StartDepotClosingResponse>;
