using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public record ChangeDepotStatusCommand(
    int Id,
    DepotStatus Status
) : IRequest<ChangeDepotStatusResponse>;
