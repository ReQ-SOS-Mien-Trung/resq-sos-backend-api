using MediatR;
using RESQ.Application.UseCases.Logistics.Thresholds;

namespace RESQ.Application.UseCases.Logistics.Queries.GetWarningBandConfig;

public record GetWarningBandConfigQuery : IRequest<WarningBandConfigResponse?>;
