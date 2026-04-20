using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;

public class MarkExternalClosureCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository)
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
                "Kho chưa có phiên đóng kho đang mở. Vui lòng gọi POST /{id}/closed trước để hệ thống kiểm tra tồn kho và khởi động bước xác nhận.");
        }

        if (closure.Status != DepotClosureStatus.InProgress || closure.ResolutionType != null)
        {
            throw new BadRequestException(
                "Phiên đóng kho không ở trạng thái chờ chọn hình thức xử lý, hoặc đã được chỉ định hình thức khác.");
        }

        closure.SetExternalResolution(request.ExternalNote, request.AdminUserId);
        await closureRepository.UpdateAsync(closure, cancellationToken);

        return new MarkExternalClosureResponse
        {
            DepotId = request.DepotId,
            ClosureId = closure.Id,
            Message = "Đã đánh dấu xử lý bên ngoài thành công. Depot manager giờ có thể gửi kết quả xử lý tồn kho lên hệ thống."
        };
    }
}
