using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;

namespace RESQ.Application.UseCases.Operations.Queries.GetMyTeamActivities;

public class GetMyTeamActivitiesQueryHandler(
    IPersonnelQueryRepository personnelQueryRepository,
    IMissionTeamRepository missionTeamRepository,
    IMissionActivityRepository activityRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    ILogger<GetMyTeamActivitiesQueryHandler> logger
) : IRequestHandler<GetMyTeamActivitiesQuery, List<MissionActivityDto>>
{
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;

    public async Task<List<MissionActivityDto>> Handle(GetMyTeamActivitiesQuery request, CancellationToken cancellationToken)
    {
        var team = await personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new ForbiddenException("Bạn chưa thuộc đội cứu hộ nào đang hoạt động.");

        var missionTeam = await missionTeamRepository.GetByMissionAndTeamAsync(request.MissionId, team.Id, cancellationToken)
            ?? throw new NotFoundException($"Đội của bạn chưa được giao cho mission #{request.MissionId}.");

        var activities = await activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        logger.LogInformation(
            "User {UserId} (Team {TeamId}, MissionTeam {MissionTeamId}) fetching their activities for Mission {MissionId}",
            request.UserId, team.Id, missionTeam.Id, request.MissionId);

        var result = activities
            .Where(a => a.MissionTeamId == missionTeam.Id)
            .Select(a => new MissionActivityDto
            {
                Id = a.Id,
                Step = a.Step,
                ActivityType = a.ActivityType,
                Description = a.Description,
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

        await MissionActivityDtoHelper.EnrichSupplyImageUrlsAsync(result, _itemModelMetadataRepository, cancellationToken);

        return result;
    }
}
