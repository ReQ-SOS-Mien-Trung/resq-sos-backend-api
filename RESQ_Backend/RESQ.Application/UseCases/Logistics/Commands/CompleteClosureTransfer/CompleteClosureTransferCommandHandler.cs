using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận đã giao toàn bộ hàng → Completed.
/// Kho đích sẽ xác nhận nhận hàng ở bước tiếp theo (/receive).
/// </summary>
public class CompleteClosureTransferCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<CompleteClosureTransferCommandHandler> logger)
    : IRequestHandler<CompleteClosureTransferCommand, CompleteClosureTransferResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    public async Task<CompleteClosureTransferResponse> Handle(
        CompleteClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        // Kiểm tra người thực hiện là manager của kho nguồn
        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != transfer.SourceDepotId)
            throw new ForbiddenException("Bạn không phải manager của kho nguồn trong quá trình chuyển hàng này.");

        // Transition: Shipping → Completed (domain validates)
        transfer.MarkCompleted(request.UserId, request.Note);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "ClosureTransfer completed (source side) | TransferId={TransferId} By={UserId}",
            transfer.Id, request.UserId);

        // Notify target manager that all goods have been dispatched
        try
        {
            var targetManagerId = await inventoryRepository.GetActiveManagerUserIdByDepotIdAsync(
                transfer.TargetDepotId, cancellationToken);

            if (targetManagerId.HasValue)
                await firebaseService.SendNotificationToUserAsync(
                    targetManagerId.Value,
                    "Kho nguồn đã giao hàng xong",
                    $"Toàn bộ {transfer.SnapshotConsumableUnits:N0} đơn vị hàng đã được giao. Vui lòng xác nhận nhận hàng.",
                    "closure_transfer_completed",
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify target manager for TransferId={Id}", transfer.Id);
        }

        return new CompleteClosureTransferResponse
        {
            TransferId = transfer.Id,
            TransferStatus = transfer.Status,
            Message = "Đã xác nhận giao hàng hoàn tất. Kho đích vui lòng xác nhận nhận hàng để hoàn tất quá trình."
        };
    }
}
