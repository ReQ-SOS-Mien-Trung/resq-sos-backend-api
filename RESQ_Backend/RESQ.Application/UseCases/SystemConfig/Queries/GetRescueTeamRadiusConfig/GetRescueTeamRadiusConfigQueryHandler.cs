using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescueTeamRadiusConfig;

public class GetRescueTeamRadiusConfigQueryHandler(
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository)
    : IRequestHandler<GetRescueTeamRadiusConfigQuery, GetRescueTeamRadiusConfigResponse>
{
    private const double DefaultMaxRadiusKm = 10.0;

    private readonly IRescueTeamRadiusConfigRepository _rescueTeamRadiusConfigRepository = rescueTeamRadiusConfigRepository;

    public async Task<GetRescueTeamRadiusConfigResponse> Handle(
        GetRescueTeamRadiusConfigQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _rescueTeamRadiusConfigRepository.GetAsync(cancellationToken);

        return new GetRescueTeamRadiusConfigResponse
        {
            MaxRadiusKm = config?.MaxRadiusKm ?? DefaultMaxRadiusKm,
            UpdatedBy = config?.UpdatedBy,
            UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
