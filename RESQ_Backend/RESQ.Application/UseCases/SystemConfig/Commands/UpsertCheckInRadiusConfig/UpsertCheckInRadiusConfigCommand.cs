using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;

public class UpsertCheckInRadiusConfigCommand : IRequest<UpsertCheckInRadiusConfigResponse>
{
    public Guid UserId { get; set; }
    public double MaxRadiusMeters { get; set; }
}
