using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescueTeamRadiusConfig;

public record GetRescueTeamRadiusConfigQuery : IRequest<GetRescueTeamRadiusConfigResponse>;
