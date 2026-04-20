using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

public class CancelDepotClosureTransferCommandHandler(
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureRepository closureRepository,
    IUnitOfWork unitOfWork,
    ILogger<CancelDepotClosureTransferCommandHandler> logger)
    : IRequestHandler<CancelDepotClosureTransferCommand, CancelDepotClosureTransferResponse>
{
    public async Task<CancelDepotClosureTransferResponse> Handle(
        CancelDepotClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CancelDepotClosureTransfer | DepotId={DepotId} TransferId={TransferId} By={By}",
            request.DepotId, request.TransferId, request.CancelledBy);

        // 1. Load transfer và validate
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        if (transfer.SourceDepotId != request.DepotId)
            throw new ConflictException("Bản ghi chuyển kho không thuộc kho nguồn này.");

        if (transfer.Status == "Received")
            throw new ConflictException("Không thể hủy vì quá trình chuyển hàng đã hoàn tất (Received).");

        if (transfer.Status == "Cancelled")
            throw new ConflictException("Bản ghi chuyển kho đã bị hủy trước đó.");

        // 2. Hủy transfer
        transfer.Cancel(request.CancelledBy, request.Reason);

        // 3. Hủy closure record liên kết (audit consistency)
        var closure = await closureRepository.GetByIdAsync(transfer.ClosureId, cancellationToken);
        var cancelledAt = DateTime.UtcNow;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);

            if (closure != null)
            {
                closure.Cancel(request.CancelledBy, cancelledAt, request.Reason ?? "Hủy phiên chuyển kho");
                await closureRepository.UpdateAsync(closure, cancellationToken);
            }

            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "DepotClosureTransfer cancelled | TransferId={T} DepotId={D}",
            transfer.Id, request.DepotId);

        return new CancelDepotClosureTransferResponse
        {
            TransferId = transfer.Id,
            DepotId = request.DepotId,
            TransferStatus = transfer.Status,
            CancelledAt = cancelledAt,
            Message = "Đã hủy phiên chuyển kho. Kho vẫn ở trạng thái Closing, admin có thể chọn phương thức xử lý khác."
        };
    }
}
