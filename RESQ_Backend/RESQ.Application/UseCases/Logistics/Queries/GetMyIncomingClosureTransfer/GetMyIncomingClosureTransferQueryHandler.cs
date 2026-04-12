using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyIncomingClosureTransfer;

public class GetMyIncomingClosureTransferQueryHandler(
    IDepotInventoryRepository inventoryRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotRepository depotRepository,
    ILogger<GetMyIncomingClosureTransferQueryHandler> logger)
    : IRequestHandler<GetMyIncomingClosureTransferQuery, MyIncomingClosureTransferResponse?>
{
    public async Task<MyIncomingClosureTransferResponse?> Handle(
        GetMyIncomingClosureTransferQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Xác định depot của manager từ token
        var myDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        // 2. Tìm transfer chưa kết thúc mà kho này là kho đích
        var transfer = await transferRepository.GetActiveIncomingByTargetDepotIdAsync(myDepotId, cancellationToken);
        if (transfer == null)
        {
            logger.LogDebug("No active incoming closure transfer for depot #{DepotId}", myDepotId);
            return null; // 204 No Content — không có phiên nào đang chờ
        }

        var transferItems = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);

        // 3. Lấy tên kho nguồn để hiển thị
        var sourceDepot = await depotRepository.GetByIdAsync(transfer.SourceDepotId, cancellationToken);

        logger.LogInformation(
            "GetMyIncomingClosureTransfer | ManagerDepot={Target} SourceDepot={Source} TransferId={T} Status={S}",
            myDepotId, transfer.SourceDepotId, transfer.Id, transfer.Status);

        return new MyIncomingClosureTransferResponse
        {
            SourceDepotId            = transfer.SourceDepotId,
            ClosureId                = transfer.ClosureId,
            TransferId               = transfer.Id,
            SourceDepotName          = sourceDepot?.Name ?? $"Kho #{transfer.SourceDepotId}",
            TransferStatus           = transfer.Status,
            SnapshotConsumableUnits  = transfer.SnapshotConsumableUnits,
            SnapshotReusableUnits    = transfer.SnapshotReusableUnits,
            CreatedAt                = transfer.CreatedAt,
            ShippedAt                = transfer.ShippedAt,
            IncomingItems            = transferItems.Select(item => new ClosureInventoryItemDto
            {
                ItemModelId = item.ItemModelId,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Unit = item.Unit ?? "N/A",
                Quantity = item.Quantity,
                TransferableQuantity = item.Quantity,
                BlockedQuantity = 0
            }).ToList()
        };
    }
}
