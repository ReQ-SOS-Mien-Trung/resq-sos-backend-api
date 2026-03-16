using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IRescueTeamRepository rescueTeamRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<AddMissionActivityCommandHandler> logger
) : IRequestHandler<AddMissionActivityCommand, AddMissionActivityResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AddMissionActivityCommandHandler> _logger = logger;

    public async Task<AddMissionActivityResponse> Handle(AddMissionActivityCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding activity to MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        var activity = new MissionActivityModel
        {
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityCode = request.ActivityCode,
            ActivityType = request.ActivityType,
            Description = request.Description,
            Target = request.Target,
            Items = request.Items,
            TargetLatitude = request.TargetLatitude,
            TargetLongitude = request.TargetLongitude,
            Status = MissionActivityStatus.Planned
        };

        var activityId = await _activityRepository.AddAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        var response = new AddMissionActivityResponse
        {
            ActivityId = activityId,
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityType = request.ActivityType,
            Status = "pending"
        };

        if (request.RescueTeamId.HasValue)
        {
            var assignCommand = new AssignTeamToMissionCommand(
                request.MissionId,
                request.RescueTeamId.Value,
                request.AssignedById
            );
            var assignResult = await _mediator.Send(assignCommand, cancellationToken);
            response.MissionTeamId = assignResult.MissionTeamId;
            response.AssignedRescueTeamId = assignResult.RescueTeamId;
        }

        return response;
    }
}
