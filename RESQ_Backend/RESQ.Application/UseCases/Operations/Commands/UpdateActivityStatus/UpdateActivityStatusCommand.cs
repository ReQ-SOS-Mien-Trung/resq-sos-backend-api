using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public record UpdateActivityStatusCommand(
    int ActivityId,
    MissionActivityStatus Status,
    Guid DecisionBy
) : IRequest<UpdateActivityStatusResponse>;
