using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;

public class AssignTeamToMissionCommandHandler(
    IMissionRepository missionRepository,
    IMissionTeamRepository missionTeamRepository,
    IRescueTeamRepository rescueTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork
) : IRequestHandler<AssignTeamToMissionCommand, AssignTeamToMissionResponse>
{
    public async Task<AssignTeamToMissionResponse> Handle(AssignTeamToMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        var team = await rescueTeamRepository.GetByIdAsync(request.RescueTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đội cứu hộ với ID: {request.RescueTeamId}");

        var now = DateTime.UtcNow;
        var missionTeamModel = new MissionTeamModel
        {
            MissionId = request.MissionId,
            RescuerTeamId = request.RescueTeamId,
            Status = MissionTeamExecutionStatus.Assigned.ToString(),
            AssignedAt = now
        };

        var missionTeamId = await missionTeamRepository.CreateAsync(missionTeamModel, cancellationToken);
        await unitOfWork.SaveAsync();

        // Update rescue team status: Available → Assigned
        await mediator.Send(new ChangeTeamMissionStateCommand(request.RescueTeamId, "assign"), cancellationToken);

        return new AssignTeamToMissionResponse
        {
            MissionTeamId = missionTeamId,
            MissionId = request.MissionId,
            RescueTeamId = request.RescueTeamId,
            Status = MissionTeamExecutionStatus.Assigned.ToString(),
            AssignedAt = now
        };
    }
}
