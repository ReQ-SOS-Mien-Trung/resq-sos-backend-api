using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;

public class UpsertRescueTeamRadiusConfigCommand : IRequest<UpsertRescueTeamRadiusConfigResponse>
{
    public Guid UserId { get; set; }
    public double MaxRadiusKm { get; set; }
}
