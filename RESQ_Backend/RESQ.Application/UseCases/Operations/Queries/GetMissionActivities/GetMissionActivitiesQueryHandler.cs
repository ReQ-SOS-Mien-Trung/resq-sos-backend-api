using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;

public class GetMissionActivitiesQueryHandler(
    IMissionActivityRepository activityRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    ILogger<GetMissionActivitiesQueryHandler> logger
) : IRequestHandler<GetMissionActivitiesQuery, List<MissionActivityDto>>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly ILogger<GetMissionActivitiesQueryHandler> _logger = logger;

    public async Task<List<MissionActivityDto>> Handle(GetMissionActivitiesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting activities for MissionId={missionId}", request.MissionId);

        var activities = await _activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        var result = activities.Select(a => new MissionActivityDto
        {
            Id = a.Id,
            Step = a.Step,
            ActivityType = a.ActivityType,
            Description = a.Description,
            ImageUrl = a.ImageUrl,
            Priority = a.Priority,
            EstimatedTime = a.EstimatedTime,
            SosRequestId = a.SosRequestId,
            DepotId = a.DepotId,
            DepotName = a.DepotName,
            DepotAddress = a.DepotAddress,
            AssemblyPointId = a.AssemblyPointId,
            AssemblyPointName = a.AssemblyPointName,
            AssemblyPointLatitude = a.AssemblyPointLatitude,
            AssemblyPointLongitude = a.AssemblyPointLongitude,
            SuppliesToCollect = MissionActivityDtoHelper.ParseSupplies(a.Items),
            TargetLatitude = a.TargetLatitude,
            TargetLongitude = a.TargetLongitude,
            Status = a.Status.ToString(),
            MissionTeamId = a.MissionTeamId,
            AssignedAt = a.AssignedAt,
            CompletedAt = a.CompletedAt,
            CompletedBy = a.CompletedBy
        }).ToList();

        MissionActivityDtoHelper.EnrichSupplyExecutionContext(activities, result);
        await MissionActivityDtoHelper.EnrichVictimContextAsync(
            result,
            _sosRequestRepository,
            _sosRequestUpdateRepository,
            cancellationToken);
        await MissionActivityDtoHelper.EnrichSupplyImageUrlsAsync(result, _itemModelMetadataRepository, cancellationToken);

        return result;
    }
}
