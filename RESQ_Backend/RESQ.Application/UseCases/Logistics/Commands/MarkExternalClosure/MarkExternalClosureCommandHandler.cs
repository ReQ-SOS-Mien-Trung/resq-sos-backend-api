using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public class MarkExternalClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    RESQ.Application.Repositories.Base.IUnitOfWork unitOfWork,
    ILogger<MarkExternalClosureCommandHandler> logger)
    : IRequestHandler<MarkExternalClosureCommand, MarkExternalClosureResponse>
{
    public async Task<MarkExternalClosureResponse> Handle(MarkExternalClosureCommand request, CancellationToken cancellationToken)
    {
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho #{request.DepotId}.");

        var closure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);

        if (closure == null)
        {
            if (depot.Status != DepotStatus.Unavailable)
                throw new BadRequestException("Kho chưa được chuyển sang trạng thái xử lý/đóng cửa (Unavailable).");

            var inventoryItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
            
            var snapshotConsumableUnits = inventoryItems.Where(i => i.ItemType == "Consumable").Sum(i => i.Quantity);
            var snapshotReusableUnits = inventoryItems.Where(i => i.ItemType == "Reusable").Sum(i => i.Quantity);
            var totalConsumableRows = inventoryItems.Count(i => i.ItemType == "Consumable");

            closure = DepotClosureRecord.Create(
                depotId: request.DepotId,
                initiatedBy: request.AdminUserId,
                closeReason: request.ExternalNote ?? "Đánh dấu xử lý bên ngoài",
                previousStatus: depot.Status,
                snapshotConsumableUnits: snapshotConsumableUnits,
                snapshotReusableUnits: snapshotReusableUnits,
                totalConsumableRows: totalConsumableRows,
                totalReusableUnits: snapshotReusableUnits);

            var id = await closureRepository.CreateAsync(closure, cancellationToken);
            closure.SetGeneratedId(id);
        }

        if (closure.Status != DepotClosureStatus.InProgress || (closure.ResolutionType != null && closure.ResolutionType != CloseResolutionType.ExternalResolution))
            throw new BadRequestException("Phiên đóng kho không ở trạng thái cần đánh dấu hình thức hoặc đã được chỉ định hình thức khác.");

        closure.SetExternalResolution(request.ExternalNote);
        await closureRepository.UpdateAsync(closure, cancellationToken);
        await unitOfWork.SaveAsync();

        // Gửi thông báo Firebase cho depot manager (best-effort, không block response)
        var managerUserId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(request.DepotId, cancellationToken);
        if (managerUserId.HasValue)
        {
            try
            {
                await firebaseService.SendNotificationToUserAsync(
                    userId: managerUserId.Value,
                    title: "Yêu cầu xử lý tồn kho bên ngoài",
                    body: $"Kho \"{depot.Name}\" được yêu cầu xử lý hàng tồn bên ngoài hệ thống. Vui lòng thực hiện và xác nhận hoàn tất.",
                    type: "depot_closure_external",
                    data: new Dictionary<string, string>
                    {
                        ["closureId"] = closure.Id.ToString(),
                        ["depotId"]   = request.DepotId.ToString()
                    },
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "MarkExternalClosure | Firebase notification sent | DepotId={DepotId} ClosureId={ClosureId} ManagerUserId={ManagerUserId}",
                    request.DepotId, closure.Id, managerUserId.Value);
            }
            catch (Exception ex)
            {
                // Không ném lỗi — notification là best-effort
                logger.LogWarning(ex,
                    "MarkExternalClosure | Failed to send Firebase notification | DepotId={DepotId} ManagerUserId={ManagerUserId}",
                    request.DepotId, managerUserId.Value);
            }
        }
        else
        {
            logger.LogWarning(
                "MarkExternalClosure | No active manager found for depot — skipping notification | DepotId={DepotId}",
                request.DepotId);
        }

        return new MarkExternalClosureResponse
        {
            DepotId   = request.DepotId,
            ClosureId = closure.Id,
            Message   = "Đã đánh dấu và thông báo cho depot manager xử lý bên ngoài thành công."
        };
    }
}
