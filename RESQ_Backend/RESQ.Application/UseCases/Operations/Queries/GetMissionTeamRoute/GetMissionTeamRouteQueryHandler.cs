using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;

public class GetMissionTeamRouteQueryHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IDepotRepository depotRepository,
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

        var hasExplicitOriginLat = request.OriginLat.HasValue;
        var hasExplicitOriginLng = request.OriginLng.HasValue;

        if (hasExplicitOriginLat != hasExplicitOriginLng)
            throw new BadRequestException("Nếu muốn ghi đè vị trí xuất phát, phải truyền đồng thời originLat và originLng.");

        if (!MissionRouteCoordinateResolver.HasUsableCoordinates(missionTeam.Latitude, missionTeam.Longitude))
            throw new BadRequestException(
                $"MissionTeamId={request.MissionTeamId} chưa có vị trí hiện tại hoặc điểm tập kết hợp lệ để tính route.");

        var originLatitude = hasExplicitOriginLat
            ? request.OriginLat!.Value
            : missionTeam.Latitude!.Value;
        var originLongitude = hasExplicitOriginLng
            ? request.OriginLng!.Value
            : missionTeam.Longitude!.Value;
        var originSource = hasExplicitOriginLat
            ? "RequestQuery"
            : (string.IsNullOrWhiteSpace(missionTeam.LocationSource) ? "MissionTeam" : missionTeam.LocationSource);

        var allActivities = await activityRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);

        var excludedStatuses = new[]
        {
            MissionActivityStatus.Succeed,
            MissionActivityStatus.Failed,
            MissionActivityStatus.Cancelled
        };

        var teamActivities = allActivities
            .Where(a => a.MissionTeamId == request.MissionTeamId
                     && !excludedStatuses.Contains(a.Status))
            .OrderBy(a => a.Step ?? int.MaxValue)
            .ThenBy(a => a.Id)
            .ToList();

        var resolvedActivities = new List<(int ActivityId, int? Step, string? ActivityType, string? Description, double Latitude, double Longitude)>();

        foreach (var activity in teamActivities)
        {
            var usesDepotCoordinates = MissionRouteCoordinateResolver.UsesDepotCoordinates(activity);
            var requiresDepotFallback = MissionRouteCoordinateResolver.RequiresDepotFallback(activity);
            var coordinates = await MissionRouteCoordinateResolver.ResolveAsync(activity, depotRepository, cancellationToken);

            if (coordinates is null)
            {
                logger.LogWarning(
                    "Skipping activity {ActivityId} in MissionId={MissionId}, MissionTeamId={MissionTeamId} because no usable route coordinates were found.",
                    activity.Id,
                    request.MissionId,
                    request.MissionTeamId);
                continue;
            }

            if (usesDepotCoordinates)
            {
                logger.LogInformation(
                    requiresDepotFallback
                        ? "Using depot coordinates for depot-linked activity {ActivityId} ({ActivityType}) in MissionId={MissionId}, MissionTeamId={MissionTeamId} because target coordinates are missing or zero."
                        : "Normalizing depot-linked activity {ActivityId} ({ActivityType}) in MissionId={MissionId}, MissionTeamId={MissionTeamId} to use the current depot coordinates.",
                    activity.Id,
                    activity.ActivityType,
                    request.MissionId,
                    request.MissionTeamId);
            }

            resolvedActivities.Add((
                activity.Id,
                activity.Step,
                activity.ActivityType,
                activity.Description,
                coordinates.Value.Latitude,
                coordinates.Value.Longitude));
        }

        if (resolvedActivities.Count == 0)
            throw new NotFoundException(
                $"Không có activity nào có tọa độ cho MissionTeamId={request.MissionTeamId} trong Mission={request.MissionId}.");

        var waypoints = resolvedActivities
            .Select(a => (a.Latitude, a.Longitude));

        var routeResult = await goongMapService.GetMissionRouteAsync(
            originLatitude,
            originLongitude,
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

        var responseLegs = routeResult.Legs
            .Select((leg, index) =>
            {
                var fromActivity = index > 0 ? resolvedActivities[index - 1] : default;
                var toActivity = index < resolvedActivities.Count ? resolvedActivities[index] : default;

                return new GoongLegSummary
                {
                    SegmentIndex = index,
                    FromStep = index == 0 ? null : fromActivity.Step,
                    ToStep = index < resolvedActivities.Count ? toActivity.Step : null,
                    FromLatitude = leg.FromLatitude,
                    FromLongitude = leg.FromLongitude,
                    ToLatitude = leg.ToLatitude,
                    ToLongitude = leg.ToLongitude,
                    OverviewPolyline = leg.OverviewPolyline,
                    DistanceMeters = leg.DistanceMeters,
                    DistanceText = leg.DistanceText,
                    DurationSeconds = leg.DurationSeconds,
                    DurationText = leg.DurationText
                };
            })
            .ToList();

        return new GetMissionTeamRouteResponse
        {
            MissionTeamId        = missionTeam.Id,
            RescueTeamId         = missionTeam.RescuerTeamId,
            TeamName             = missionTeam.TeamName,
            TeamCode             = missionTeam.TeamCode,
            MissionTeamStatus    = missionTeam.Status,
            RescueTeamStatus     = missionTeam.TeamStatus,
            TeamLatitude         = missionTeam.Latitude,
            TeamLongitude        = missionTeam.Longitude,
            TeamLocationUpdatedAt = missionTeam.LocationUpdatedAt,
            TeamLocationSource   = missionTeam.LocationSource,
            OriginLatitude       = originLatitude,
            OriginLongitude      = originLongitude,
            OriginSource         = originSource,
            Status               = routeResult.Status,
            ErrorMessage         = routeResult.ErrorMessage,
            TotalDistanceMeters  = routeResult.TotalDistanceMeters,
            TotalDurationSeconds = routeResult.TotalDurationSeconds,
            OverviewPolyline     = routeResult.OverviewPolyline,
            Legs                 = responseLegs,
            Waypoints            = resolvedActivities.Select(a => new MissionRouteWaypoint
            {
                ActivityId   = a.ActivityId,
                Step         = a.Step,
                ActivityType = a.ActivityType,
                Description  = a.Description,
                Latitude     = a.Latitude,
                Longitude    = a.Longitude
            }).ToList()
        };
    }
}
