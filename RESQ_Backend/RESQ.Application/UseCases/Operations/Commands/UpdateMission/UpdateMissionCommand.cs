using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public record UpdateMissionCommand(
    int MissionId,
    string? MissionType,
    double? PriorityScore,
    DateTime? StartTime,
    DateTime? ExpectedEndTime
) : IRequest<UpdateMissionResponse>;
