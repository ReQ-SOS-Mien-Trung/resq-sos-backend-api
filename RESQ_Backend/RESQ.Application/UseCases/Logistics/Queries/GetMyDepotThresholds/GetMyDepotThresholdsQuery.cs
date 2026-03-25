using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;

public record GetMyDepotThresholdsQuery(Guid UserId) : IRequest<GetMyDepotThresholdsResponse>;
