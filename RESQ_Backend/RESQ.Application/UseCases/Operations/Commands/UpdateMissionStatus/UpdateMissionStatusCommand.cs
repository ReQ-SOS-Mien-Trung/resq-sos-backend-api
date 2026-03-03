using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public record UpdateMissionStatusCommand(
    int MissionId,
    MissionStatus Status
) : IRequest<UpdateMissionStatusResponse>;
