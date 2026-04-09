using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;

/// <summary>
/// Quản lý kho nguồn xác nhận đang chuẩn bị hàng → chuyển transfer sang Preparing.
/// Đây là bước đầu tiên source manager phải thực hiện sau khi admin tạo yêu cầu chuyển kho.
/// </summary>
public class PrepareClosureTransferCommandHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<PrepareClosureTransferCommandHandler> logger)
    : IRequestHandler<PrepareClosureTransferCommand, PrepareClosureTransferResponse>
{
    public async Task<PrepareClosureTransferResponse> Handle(
        PrepareClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        if (transfer.SourceDepotId != request.DepotId)
            throw new ConflictException("Bản ghi chuyển kho không thuộc kho nguồn này.");

        // Kiểm tra người thực hiện là manager của kho nguồn
        var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != transfer.SourceDepotId)
            throw new ForbiddenException("Bạn không phải manager của kho nguồn trong quá trình chuyển hàng này.");

        // Transition: AwaitingPreparation → Preparing (domain validates)
        transfer.MarkPreparing(request.UserId, request.Note);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "ClosureTransfer preparing | TransferId={TransferId} By={UserId}",
            transfer.Id, request.UserId);

        return new PrepareClosureTransferResponse
        {
            TransferId = transfer.Id,
            TransferStatus = transfer.Status,
            Message = "Đã xác nhận chuẩn bị hàng. Tiến hành đóng gói và xuất kho khi sẵn sàng."
        };
    }
}
