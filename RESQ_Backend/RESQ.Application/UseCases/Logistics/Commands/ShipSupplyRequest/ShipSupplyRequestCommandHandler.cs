using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;

/// <summary>
/// Kho nguồn xuất hàng (TransferOut) và chuyển trạng thái sang Shipping (đang vận chuyển).
/// Inventory source depot giảm tương ứng.
/// </summary>
public class ShipSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<ShipSupplyRequestCommand, ShipSupplyRequestResponse>
{
    public async Task<ShipSupplyRequestResponse> Handle(ShipSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanShip(sr.SourceStatus);

        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new BadRequestException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        // Xuất kho — giảm tồn kho kho nguồn
        await supplyRequestRepository.TransferOutAsync(
            sr.SourceDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Shipping", "InTransit", null, cancellationToken);

        // Notify requesting manager
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Vật tư đang được vận chuyển",
            $"Yêu cầu #{sr.Id}: hàng đã xuất kho và đang vận chuyển đến kho của bạn.",
            "supply_shipped",
            cancellationToken);

        return new ShipSupplyRequestResponse { Message = $"Đã xuất hàng cho yêu cầu #{sr.Id}." };
    }
}
