using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

/// <summary>
/// Query lấy tuyến đường từ vị trí của rescuer đến địa điểm đích trong một mission activity.
/// </summary>
public record GetRescuerRouteQuery(
    int MissionId,
    int ActivityId,
    double OriginLat,
    double OriginLng,
    string Vehicle = "car"
) : IRequest<GetRescuerRouteResponse>;
