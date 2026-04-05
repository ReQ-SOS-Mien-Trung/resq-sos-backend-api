using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;

public class GetMissionTeamRouteQueryHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IGoongMapService goongMapService,
    ILogger<GetMissionTeamRouteQueryHandler> logger
) : IRequestHandler<GetMissionTeamRouteQuery, GetMissionTeamRouteResponse>
{
    public async Task<GetMissionTeamRouteResponse> Handle(GetMissionTeamRouteQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Getting mission route for MissionId={MissionId}, MissionTeamId={MissionTeamId}",
            request.MissionId,
            request.MissionTeamId);

        var missionTeam = await missionTeamRepository.GetByIdAsync(request.MissionTeamId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy liên kết đội-mission với ID: {request.MissionTeamId}");

        if (missionTeam.MissionId != request.MissionId)
            throw new BadRequestException("Mission team không thuộc mission được yêu cầu.");

        var allActivities = await activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        var excludedStatuses = new[]
        {
            MissionActivityStatus.Succeed,
            MissionActivityStatus.Failed,
            MissionActivityStatus.Cancelled
        };

        var teamActivities = allActivities
            .Where(a => a.MissionTeamId == request.MissionTeamId
                     && a.TargetLatitude.HasValue
                     && a.TargetLongitude.HasValue
                     && !excludedStatuses.Contains(a.Status))
            .OrderBy(a => a.Step ?? int.MaxValue)
            .ToList();

        if (teamActivities.Count == 0)
            throw new NotFoundException(
                $"Không có activity nào có tọa độ cho MissionTeamId={request.MissionTeamId} trong Mission={request.MissionId}.");

        var waypoints = teamActivities
            .Select(a => (a.TargetLatitude!.Value, a.TargetLongitude!.Value));

        var routeResult = await goongMapService.GetMissionRouteAsync(
            request.OriginLat,
            request.OriginLng,
            waypoints,
            request.Vehicle,
            cancellationToken);

        if (routeResult.Status != "OK")
            logger.LogWarning(
                "Goong API returned status={Status} for MissionId={MissionId}, MissionTeamId={MissionTeamId}. Error: {Error}",
                routeResult.Status,
                request.MissionId,
                request.MissionTeamId,
                routeResult.ErrorMessage);

        return new GetMissionTeamRouteResponse
        {
            Status               = routeResult.Status,
            ErrorMessage         = routeResult.ErrorMessage,
            TotalDistanceMeters  = routeResult.TotalDistanceMeters,
            TotalDurationSeconds = routeResult.TotalDurationSeconds,
            OverviewPolyline     = routeResult.OverviewPolyline,
            Legs                 = routeResult.Legs,
            Waypoints            = teamActivities.Select(a => new MissionRouteWaypoint
            {
                ActivityId   = a.Id,
                Step         = a.Step,
                ActivityType = a.ActivityType,
                Description  = a.Description,
                Latitude     = a.TargetLatitude!.Value,
                Longitude    = a.TargetLongitude!.Value
            }).ToList()
        };
    }
}
