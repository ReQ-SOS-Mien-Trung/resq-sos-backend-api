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
            .ToList();

        var resolvedActivities = new List<(int ActivityId, int? Step, string? ActivityType, string? Description, double Latitude, double Longitude)>();

        foreach (var activity in teamActivities)
        {
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

            if (requiresDepotFallback)
            {
                logger.LogInformation(
                    "Using depot coordinates for COLLECT_SUPPLIES activity {ActivityId} in MissionId={MissionId}, MissionTeamId={MissionTeamId}.",
                    activity.Id,
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
