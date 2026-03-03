using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;

public class UpdateMissionActivityCommandHandler(
    IMissionActivityRepository activityRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionActivityCommandHandler> logger
) : IRequestHandler<UpdateMissionActivityCommand, UpdateMissionActivityResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionActivityCommandHandler> _logger = logger;

    public async Task<UpdateMissionActivityResponse> Handle(UpdateMissionActivityCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating ActivityId={activityId}", request.ActivityId);

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
            throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        activity.Step = request.Step;
        activity.ActivityCode = request.ActivityCode;
        activity.ActivityType = request.ActivityType;
        activity.Description = request.Description;
        activity.Target = request.Target;
        activity.Items = request.Items;
        activity.TargetLatitude = request.TargetLatitude;
        activity.TargetLongitude = request.TargetLongitude;

        await _activityRepository.UpdateAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateMissionActivityResponse
        {
            ActivityId = activity.Id,
            MissionId = activity.MissionId,
            Step = activity.Step,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            Status = activity.Status.ToString()
        };
    }
}
