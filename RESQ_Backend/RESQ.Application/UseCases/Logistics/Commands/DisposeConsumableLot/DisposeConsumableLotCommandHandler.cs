using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.DisposeConsumableLot;

public class DisposeConsumableLotCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<DisposeConsumableLotCommand, DisposeConsumableLotResponse>
{
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;

    public async Task<DisposeConsumableLotResponse> Handle(DisposeConsumableLotCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngừng hoạt động hoặc đã đóng. Không thể tiêu hủy vật phẩm trong kho này.");

        await _depotInventoryRepository.DisposeConsumableLotAsync(
            depotId,
            request.LotId,
            request.Quantity,
            request.Reason,
            request.Note,
            request.UserId,
            cancellationToken);

        await _firebaseService.SendNotificationToUserAsync(
            request.UserId,
            "Tiêu hủy vật phẩm tiêu thụ",
            $"Đã tiêu hủy {request.Quantity} đơn vị từ lô #{request.LotId}. Nhóm lý do: {request.Reason}.",
            "inventory_disposal",
            new Dictionary<string, string>
            {
                ["lotId"] = request.LotId.ToString(),
                ["reason"] = request.Reason
            },
            cancellationToken);

        return new DisposeConsumableLotResponse(
            $"Đã tiêu hủy {request.Quantity} đơn vị từ lô #{request.LotId}. Nhóm lý do: {request.Reason}.");
    }
}
