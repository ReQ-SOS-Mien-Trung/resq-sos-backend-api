using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetCheckInRadiusConfig;

public record GetCheckInRadiusConfigQuery : IRequest<GetCheckInRadiusConfigResponse>;
