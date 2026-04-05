using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;
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

        var routeStops = BuildRouteStops(teamActivities);

        var routeResult = await goongMapService.GetMissionRouteAsync(
            request.OriginLat,
            request.OriginLng,
            routeStops.Select(stop => (stop.Latitude, stop.Longitude)),
            request.Vehicle,
            cancellationToken);

        if (routeResult.Legs.Count != routeStops.Count)
            logger.LogWarning(
                "Mission route leg count mismatch for MissionId={MissionId}, MissionTeamId={MissionTeamId}. Expected={Expected}, Actual={Actual}",
                request.MissionId,
                request.MissionTeamId,
                routeStops.Count,
                routeResult.Legs.Count);

        for (var i = 0; i < routeResult.Legs.Count && i < routeStops.Count; i++)
        {
            routeResult.Legs[i].FromStep = i == 0 ? null : routeStops[i - 1].EndStep;
            routeResult.Legs[i].ToStep = routeStops[i].StartStep;
        }

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

    private static List<RouteStop> BuildRouteStops(IEnumerable<MissionActivityModel> teamActivities)
    {
        var routeStops = new List<RouteStop>();

        foreach (var activity in teamActivities)
        {
            var latitude = activity.TargetLatitude!.Value;
            var longitude = activity.TargetLongitude!.Value;

            if (routeStops.Count > 0 && AreSameLocation(routeStops[^1].Latitude, routeStops[^1].Longitude, latitude, longitude))
            {
                if (routeStops[^1].StartStep is null && activity.Step is int startStep)
                    routeStops[^1].StartStep = startStep;

                if (activity.Step is int endStep)
                    routeStops[^1].EndStep = endStep;

                continue;
            }

            routeStops.Add(new RouteStop
            {
                StartStep = activity.Step,
                EndStep = activity.Step,
                Latitude = latitude,
                Longitude = longitude
            });
        }

        return routeStops;
    }

    private static bool AreSameLocation(double leftLat, double leftLng, double rightLat, double rightLng)
        => Math.Abs(leftLat - rightLat) < 0.000001 && Math.Abs(leftLng - rightLng) < 0.000001;

    private sealed class RouteStop
    {
        public int? StartStep { get; set; }
        public int? EndStep { get; set; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
    }
}
