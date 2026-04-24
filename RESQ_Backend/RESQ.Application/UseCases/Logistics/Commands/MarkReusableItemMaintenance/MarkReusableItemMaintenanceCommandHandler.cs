using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemMaintenance;

public class MarkReusableItemMaintenanceCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<MarkReusableItemMaintenanceCommand, MarkReusableItemMaintenanceResponse>
{
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;

    public async Task<MarkReusableItemMaintenanceResponse> Handle(
        MarkReusableItemMaintenanceCommand request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.UserId,
                request.DepotId,
                cancellationToken)
            ?? throw new ForbiddenException("Bạn không có quyền thao tác với kho này.");

        var itemDepotId = await _depotInventoryRepository.GetReusableItemDepotIdAsync(
                request.ReusableItemId,
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy vật phẩm tái sử dụng #{request.ReusableItemId}.");

        if (itemDepotId != depotId)
        {
            throw new NotFoundException(
                $"Không tìm thấy vật phẩm tái sử dụng #{request.ReusableItemId} trong kho #{request.DepotId}.");
        }

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);

        // Kho Unavailable vẫn được phép bảo trì/tái kiểm tra nội bộ.
        // Chỉ chặn các trạng thái chưa vận hành hoặc đang trong luồng đóng kho.
        if (depotStatus is DepotStatus.Created or DepotStatus.PendingAssignment or DepotStatus.Closing or DepotStatus.Closed)
        {
            throw new ConflictException(
                "Kho hiện không ở trạng thái cho phép cập nhật bảo trì vật phẩm tái sử dụng. " +
                "Chỉ cho phép khi kho đang vận hành hoặc tạm ngừng (Unavailable).");
        }

        await _depotInventoryRepository.MarkReusableItemMaintenanceAsync(
            depotId,
            request.ReusableItemId,
            request.Note,
            request.UserId,
            cancellationToken);

        return new MarkReusableItemMaintenanceResponse(
            $"Đã chuyển vật phẩm tái sử dụng #{request.ReusableItemId} sang trạng thái bảo trì.");
    }
}
