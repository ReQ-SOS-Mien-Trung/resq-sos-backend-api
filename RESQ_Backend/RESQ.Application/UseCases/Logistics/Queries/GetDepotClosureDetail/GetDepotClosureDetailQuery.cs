using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public record GetDepotClosureDetailQuery(
    int DepotId,
    int ClosureId,
    Guid? RequestingUserId = null) : IRequest<DepotClosureDetailResponse>;
