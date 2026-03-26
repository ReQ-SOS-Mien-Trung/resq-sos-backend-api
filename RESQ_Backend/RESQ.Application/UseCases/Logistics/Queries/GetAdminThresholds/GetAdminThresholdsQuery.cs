using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAdminThresholds;

public record GetAdminThresholdsQuery(int? DepotId) : IRequest<GetAdminThresholdsResponse>;
