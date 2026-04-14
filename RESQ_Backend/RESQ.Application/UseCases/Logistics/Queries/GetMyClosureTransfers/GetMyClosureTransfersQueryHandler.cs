using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyClosureTransfers;

public class GetMyClosureTransfersQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository inventoryRepository,
    IDepotClosureTransferRepository transferRepository)
    : IRequestHandler<GetMyClosureTransfersQuery, List<MyClosureTransferDto>>
{
    public async Task<List<MyClosureTransferDto>> Handle(GetMyClosureTransfersQuery request, CancellationToken cancellationToken)
    {
        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);

        int depotId;
        if (request.DepotId.HasValue)
        {
            if (managerDepotId.HasValue && managerDepotId.Value != request.DepotId.Value)
                throw new ForbiddenException("B?n ch? c� th? xem transfer c?a kho m�nh qu?n l�.");

            depotId = request.DepotId.Value;
        }
        else
        {
            depotId = managerDepotId
                ?? throw new NotFoundException("B?n hi?n kh�ng ph? tr�ch kho n�o.");
        }

        var transfers = await transferRepository.GetByRelatedDepotIdAsync(depotId, cancellationToken);

        return transfers.Select(transfer =>
        {
            var isSourceDepot = transfer.SourceDepotId == depotId;

            return new MyClosureTransferDto
            {
                TransferId = transfer.TransferId,
                ClosureId = transfer.ClosureId,
                SourceDepotId = transfer.SourceDepotId,
                SourceDepotName = transfer.SourceDepotName,
                TargetDepotId = transfer.TargetDepotId,
                TargetDepotName = transfer.TargetDepotName,
                Status = transfer.Status,
                UserRole = isSourceDepot ? "SourceDepot" : "TargetDepot",
                RelatedDepotId = depotId,
                RelatedDepotName = isSourceDepot ? transfer.SourceDepotName : transfer.TargetDepotName,
                CounterpartyDepotId = isSourceDepot ? transfer.TargetDepotId : transfer.SourceDepotId,
                CounterpartyDepotName = isSourceDepot ? transfer.TargetDepotName : transfer.SourceDepotName,
                CreatedAt = transfer.CreatedAt,
                SnapshotConsumableUnits = transfer.SnapshotConsumableUnits,
                SnapshotReusableUnits = transfer.SnapshotReusableUnits,
                ShippedAt = transfer.ShippedAt,
                ReceivedAt = transfer.ReceivedAt,
                CancelledAt = transfer.CancelledAt
            };
        }).ToList();
    }
}
