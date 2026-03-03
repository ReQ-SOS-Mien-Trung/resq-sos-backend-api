using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;

public class GetMissionActivitiesQueryHandler(
    IMissionActivityRepository activityRepository,
    ILogger<GetMissionActivitiesQueryHandler> logger
) : IRequestHandler<GetMissionActivitiesQuery, List<MissionActivityDto>>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly ILogger<GetMissionActivitiesQueryHandler> _logger = logger;

    public async Task<List<MissionActivityDto>> Handle(GetMissionActivitiesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting activities for MissionId={missionId}", request.MissionId);

        var activities = await _activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        return activities.Select(a => new MissionActivityDto
        {
            Id = a.Id,
            Step = a.Step,
            ActivityCode = a.ActivityCode,
            ActivityType = a.ActivityType,
            Description = a.Description,
            Target = a.Target,
            Items = a.Items,
            TargetLatitude = a.TargetLatitude,
            TargetLongitude = a.TargetLongitude,
            Status = a.Status.ToString(),
            AssignedAt = a.AssignedAt,
            CompletedAt = a.CompletedAt
        }).ToList();
    }
}
