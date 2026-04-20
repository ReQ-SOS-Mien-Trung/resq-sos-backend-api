using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.MarkReusableItemAvailable;

public class MarkReusableItemAvailableCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<MarkReusableItemAvailableCommand, MarkReusableItemAvailableResponse>
{
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;

    public async Task<MarkReusableItemAvailableResponse> Handle(
        MarkReusableItemAvailableCommand request,
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
            throw new NotFoundException(
                $"Không tìm thấy vật phẩm tái sử dụng #{request.ReusableItemId} trong kho #{request.DepotId}.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Created or DepotStatus.PendingAssignment or DepotStatus.Closed or DepotStatus.Closing)
            throw new ConflictException("Kho hiện không ở trạng thái cho phép cập nhật bảo trì vật phẩm tái sử dụng.");

        await _depotInventoryRepository.MarkReusableItemAvailableAsync(
            depotId,
            request.ReusableItemId,
            request.Note,
            request.UserId,
            cancellationToken);

        return new MarkReusableItemAvailableResponse(
            $"Đã chuyển vật phẩm tái sử dụng #{request.ReusableItemId} về trạng thái sẵn sàng.");
    }
}
