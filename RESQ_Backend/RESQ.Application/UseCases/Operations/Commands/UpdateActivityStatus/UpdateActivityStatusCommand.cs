using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public record UpdateActivityStatusCommand(
    int MissionId,
    int ActivityId,
    MissionActivityStatus Status,
    Guid DecisionBy,
    string? ImageUrl = null
) : IRequest<UpdateActivityStatusResponse>;
