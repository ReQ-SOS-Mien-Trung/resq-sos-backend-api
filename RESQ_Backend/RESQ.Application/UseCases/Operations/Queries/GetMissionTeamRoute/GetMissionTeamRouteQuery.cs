using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;

public record GetMissionTeamRouteQuery(
    int MissionId,
    int MissionTeamId,
    double OriginLat,
    double OriginLng,
    string Vehicle = "car"
) : IRequest<GetMissionTeamRouteResponse>;
