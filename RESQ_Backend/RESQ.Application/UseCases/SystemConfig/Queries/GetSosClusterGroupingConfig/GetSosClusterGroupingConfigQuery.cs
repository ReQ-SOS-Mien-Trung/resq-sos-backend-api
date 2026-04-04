using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosClusterGroupingConfig;

public record GetSosClusterGroupingConfigQuery : IRequest<GetSosClusterGroupingConfigResponse>;