using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public record GetDepotClosureDetailQuery(int DepotId, int ClosureId) : IRequest<DepotClosureDetailResponse>;
