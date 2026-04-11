using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;

public class CancelDepotClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IUnitOfWork unitOfWork,
    ILogger<CancelDepotClosureCommandHandler> logger)
    : IRequestHandler<CancelDepotClosureCommand, CancelDepotClosureResponse>
{
    public async Task<CancelDepotClosureResponse> Handle(
        CancelDepotClosureCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CancelDepotClosure | ClosureId={ClosureId} DepotId={DepotId} CancelledBy={By}",
            request.ClosureId, request.DepotId, request.CancelledBy);

        // 1. Load và validate closure
        var closure = await closureRepository.GetByIdAsync(request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi đóng kho.");

        if (closure.DepotId != request.DepotId)
            throw new ConflictException("Bản ghi đóng kho không thuộc kho này.");

        if (closure.Status != DepotClosureStatus.InProgress && closure.Status != DepotClosureStatus.Processing)
            throw new ConflictException(
                $"Không thể huỷ — bản ghi đóng kho đang ở trạng thái '{closure.Status}'.");

        // 2. Atomic claim để tránh race condition với request xử lý đồng thời
        //    Nếu Processing (bị kẹt từ lần trước): dùng force-claim bằng rowVersion
        //    Nếu InProgress: dùng claim thông thường
        bool claimed;
        if (closure.Status == DepotClosureStatus.Processing)
        {
            claimed = await closureRepository.TryForceClaimFromProcessingAsync(
                request.ClosureId, closure.RowVersion, cancellationToken);
        }
        else
        {
            claimed = await closureRepository.TryClaimForProcessingAsync(request.ClosureId, cancellationToken);
        }

        if (!claimed)
            throw new ConflictException("Bản ghi đóng kho đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");

        // 3. Load kho và validate
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException("Kho không ở trạng thái Unavailable.");

        var cancelledAt = DateTime.UtcNow;
        var restoredStatus = closure.PreviousStatus;

        // 4. Khôi phục kho và huỷ closure trong transaction
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            depot.RestoreFromClosing(restoredStatus);
            closure.Cancel(request.CancelledBy, cancelledAt, request.CancellationReason);

            await depotRepository.UpdateAsync(depot, cancellationToken);
            await closureRepository.UpdateAsync(closure, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "DepotClosure Cancelled | ClosureId={ClosureId} DepotId={DepotId} RestoredStatus={Status}",
            request.ClosureId, request.DepotId, restoredStatus);

        return new CancelDepotClosureResponse
        {
            ClosureId = request.ClosureId,
            DepotId = request.DepotId,
            RestoredStatus = restoredStatus.ToString(),
            CancelledAt = cancelledAt,
            Message = $"Đã huỷ yêu cầu đóng kho. Kho đã được khôi phục về trạng thái '{restoredStatus}'."
        };
    }
}
