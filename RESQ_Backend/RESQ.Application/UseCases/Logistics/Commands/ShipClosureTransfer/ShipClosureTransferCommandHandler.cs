using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận bắt đầu vận chuyển → chuyển transfer sang Shipping.
/// Yêu cầu người thực hiện phải là manager của kho nguồn trong transfer.
/// </summary>
public class ShipClosureTransferCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<ShipClosureTransferCommandHandler> logger)
    : IRequestHandler<ShipClosureTransferCommand, ShipClosureTransferResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    public async Task<ShipClosureTransferResponse> Handle(
        ShipClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        // Kiểm tra người thực hiện là manager của kho nguồn
        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != transfer.SourceDepotId)
            throw new ForbiddenException("Bạn không phải manager của kho nguồn trong quá trình chuyển hàng này.");

        // Transition: Preparing → Shipping (domain validates)
        transfer.MarkShipping(request.UserId, request.Note);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "ClosureTransfer shipped | TransferId={TransferId} By={UserId}",
            transfer.Id, request.UserId);

        // Notify target manager
        try
        {
            var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                transfer.TargetDepotId, cancellationToken);

            if (targetManagerId.HasValue)
                await firebaseService.SendNotificationToUserAsync(
                    targetManagerId.Value,
                    "Hàng chuyển kho đang trên đường đến",
                    $"Kho nguồn đã xuất hàng. Vui lòng chuẩn bị tiếp nhận {transfer.SnapshotConsumableUnits:N0} đơn vị tài sản.",
                    "closure_transfer_shipped",
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify target manager for TransferId={Id}", transfer.Id);
        }

        return new ShipClosureTransferResponse
        {
            TransferId = transfer.Id,
            TransferStatus = transfer.Status,
            Message = "Đã bắt đầu vận chuyển. Kho đích đang được thông báo để chuẩn bị nhận hàng."
        };
    }
}
