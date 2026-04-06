using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

public class GetRescuerRouteQueryHandler(
    IMissionActivityRepository activityRepository,
    IDepotRepository depotRepository,
    IGoongMapService goongMapService,
    ILogger<GetRescuerRouteQueryHandler> logger
) : IRequestHandler<GetRescuerRouteQuery, GetRescuerRouteResponse>
{
    public async Task<GetRescuerRouteResponse> Handle(GetRescuerRouteQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Getting rescue route for MissionId={MissionId}, ActivityId={ActivityId}",
            request.MissionId,
            request.ActivityId);

        var activity = await activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Activity {request.ActivityId} không tồn tại.");

        if (activity.MissionId != request.MissionId)
            throw new BadRequestException("Activity không thuộc mission được yêu cầu.");

        var requiresDepotFallback = MissionRouteCoordinateResolver.RequiresDepotFallback(activity);
        var destination = await MissionRouteCoordinateResolver.ResolveAsync(activity, depotRepository, cancellationToken);

        if (destination is null)
            throw new BadRequestException($"Activity {request.ActivityId} chưa có tọa độ đích hợp lệ.");

        if (requiresDepotFallback)
        {
            logger.LogInformation(
                "Using depot coordinates for COLLECT_SUPPLIES activity {ActivityId} in MissionId={MissionId}.",
                request.ActivityId,
                request.MissionId);
        }

        var destLat = destination.Value.Latitude;
        var destLng = destination.Value.Longitude;

        var routeResult = await goongMapService.GetRouteAsync(
            request.OriginLat,
            request.OriginLng,
            destLat,
            destLng,
            request.Vehicle,
            cancellationToken);

        if (routeResult.Status != "OK" || routeResult.Route is null)
            logger.LogWarning("Goong API returned status={Status} for ActivityId={ActivityId}. Error: {Error}",
                routeResult.Status, request.ActivityId, routeResult.ErrorMessage);

        return new GetRescuerRouteResponse
        {
            ActivityId = activity.Id,
            ActivityType = activity.ActivityType ?? string.Empty,
            Description = activity.Description,
            DestinationLatitude = destLat,
            DestinationLongitude = destLng,
            OriginLatitude = request.OriginLat,
            OriginLongitude = request.OriginLng,
            Vehicle = request.Vehicle,
            Status = routeResult.Status,
            ErrorMessage = routeResult.ErrorMessage,
            Route = routeResult.Route
        };
    }
}
