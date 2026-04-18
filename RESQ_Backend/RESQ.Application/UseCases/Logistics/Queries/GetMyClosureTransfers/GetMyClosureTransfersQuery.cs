using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyClosureTransfers;

public record GetMyClosureTransfersQuery(Guid UserId, int? DepotId = null) : IRequest<List<MyClosureTransferDto>>;
