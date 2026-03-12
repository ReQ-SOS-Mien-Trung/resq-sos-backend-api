using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

public class GetRescuerRouteQueryHandler(
    IMissionActivityRepository activityRepository,
    IGoongMapService goongMapService,
    ILogger<GetRescuerRouteQueryHandler> logger
) : IRequestHandler<GetRescuerRouteQuery, GetRescuerRouteResponse>
{
    public async Task<GetRescuerRouteResponse> Handle(GetRescuerRouteQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting rescue route for ActivityId={ActivityId}", request.ActivityId);

        var activity = await activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Activity {request.ActivityId} không tồn tại.");

        if (activity.TargetLatitude is null || activity.TargetLongitude is null)
            throw new BadRequestException($"Activity {request.ActivityId} chưa có tọa độ đích.");

        var destLat = activity.TargetLatitude.Value;
        var destLng = activity.TargetLongitude.Value;

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
            Route = routeResult.Route
        };
    }
}
