using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public class MarkExternalClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IUnitOfWork unitOfWork,
    IOperationalHubService operationalHubService)
    : IRequestHandler<MarkExternalClosureCommand, MarkExternalClosureResponse>
{
    public async Task<MarkExternalClosureResponse> Handle(
        MarkExternalClosureCommand request,
        CancellationToken cancellationToken)
    {
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
            throw new NotFoundException($"Không tìm thấy kho #{request.DepotId}.");

        if (depot.Status != DepotStatus.Closing)
        {
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Chỉ có thể đánh dấu xử lý bên ngoài khi kho đang Closing.");
        }

        var closure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);
        if (closure == null)
        {
            throw new BadRequestException(
                "Kho chưa có phiên đóng kho đang mở. Vui lòng gọi POST /{id}/closed trước để hệ thống kiểm tra tồn kho và khởi động phiên đóng kho.");
        }

        if (closure.Status == DepotClosureStatus.Processing)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại đang được xử lý bởi tiến trình khác. Vui lòng thử lại sau.");
        }

        var hasOpenTransfers = await transferRepository.HasOpenTransfersAsync(closure.Id, cancellationToken);
        if (hasOpenTransfers)
        {
            throw new ConflictException(
                "Không thể đánh dấu xử lý bên ngoài khi vẫn còn transfer đang mở. Vui lòng hoàn tất hoặc hủy toàn bộ transfer hiện tại trước.");
        }

        if (closure.ResolutionType == CloseResolutionType.ExternalResolution)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại đã được đánh dấu xử lý bên ngoài.");
        }

        var remainingItems = await depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        if (remainingItems.Count == 0)
        {
            throw new ConflictException(
                "Kho hiện không còn hàng tồn để xử lý bên ngoài. Admin có thể xác nhận đóng kho ngay.");
        }

        closure.SetExternalResolution(request.ExternalNote, request.AdminUserId);
        await closureRepository.UpdateAsync(closure, cancellationToken);
        await unitOfWork.SaveAsync();

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = request.DepotId,
                ClosureId = closure.Id,
                EntityType = "Closure",
                Action = "MarkedExternalResolution",
                Status = closure.Status.ToString()
            },
            cancellationToken);

        return new MarkExternalClosureResponse
        {
            DepotId = request.DepotId,
            ClosureId = closure.Id,
            ClosureStatus = closure.Status.ToString(),
            ResolutionType = CloseResolutionType.ExternalResolution.ToString(),
            RemainingItemCount = remainingItems.Count,
            Message = "Đã đánh dấu xử lý bên ngoài thành công. Depot manager giờ có thể gửi kết quả xử lý phần hàng còn lại lên hệ thống."
        };
    }
}
