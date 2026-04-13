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
    public async Task<MarkExternalClosureResponse> Handle(MarkExternalClosureCommand request, CancellationToken cancellationToken)
    {
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
            throw new NotFoundException($"Không tìm thấy kho #{request.DepotId}.");

        var closure = await closureRepository.GetActiveClosureByDepotIdAsync(request.DepotId, cancellationToken);
        
        if (closure == null)
        {
            throw new BadRequestException("Kho không có tiến trình đóng nào đang chờ quyết định xử lý.");
        }
        
        if (closure.Status != DepotClosureStatus.InProgress || closure.ResolutionType != null) {
            throw new BadRequestException("Phiên đóng kho không ở trạng thái cần đánh dấu hình thức hoặc đã được chỉ định hình thức khác.");
        }

        closure.SetExternalResolution(request.ExternalNote);

        await closureRepository.UpdateAsync(closure, cancellationToken);

        return new MarkExternalClosureResponse
        {
            DepotId = request.DepotId,
            ClosureId = closure.Id,
            Message = "Đã đánh dấu báo tin thành công, depot manager giờ có thể xử lý việc thông báo số lượng bên ngoài."
        };
    }
}
