using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository activityRepository,
    IUnitOfWork unitOfWork,
    ILogger<AddMissionActivityCommandHandler> logger
) : IRequestHandler<AddMissionActivityCommand, AddMissionActivityResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
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
            Status = MissionActivityStatus.Pending
        };

        var activityId = await _activityRepository.AddAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new AddMissionActivityResponse
        {
            ActivityId = activityId,
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityType = request.ActivityType,
            Status = "pending"
        };
    }
}
