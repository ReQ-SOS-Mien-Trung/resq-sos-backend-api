using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public record UpdateMissionStatusCommand(
    int MissionId,
    string Status
) : IRequest<UpdateMissionStatusResponse>;
