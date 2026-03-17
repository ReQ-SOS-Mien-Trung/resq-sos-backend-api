using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IPersonnelQueryRepository personnelQueryRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateActivityStatusCommandHandler> logger
) : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IPersonnelQueryRepository _personnelQueryRepository = personnelQueryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateActivityStatusCommandHandler> _logger = logger;

    public async Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for ActivityId={activityId} -> {status}", request.ActivityId, request.Status);

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
            throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        // If the activity is assigned to a specific team, enforce that the requester belongs to that team
        if (activity.MissionTeamId.HasValue)
        {
            var userTeam = await _personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(request.DecisionBy, cancellationToken);
            if (userTeam is not null)
            {
                var missionTeam = await _missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
                if (missionTeam is not null && missionTeam.RescuerTeamId != userTeam.Id)
                    throw new ForbiddenException("Bạn không có quyền cập nhật trạng thái activity này. Activity được giao cho đội khác.");
            }
        }

        MissionActivityStateMachine.EnsureValidTransition(activity.Status, request.Status);

        await _activityRepository.UpdateStatusAsync(request.ActivityId, request.Status, request.DecisionBy, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateActivityStatusResponse
        {
            ActivityId = request.ActivityId,
            Status = request.Status.ToString(),
            DecisionBy = request.DecisionBy
        };
    }
}
