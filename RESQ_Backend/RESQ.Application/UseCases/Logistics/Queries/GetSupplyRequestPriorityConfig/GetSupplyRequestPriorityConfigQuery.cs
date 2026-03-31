using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequestPriorityConfig;

public record GetSupplyRequestPriorityConfigQuery : IRequest<GetSupplyRequestPriorityConfigResponse>;
