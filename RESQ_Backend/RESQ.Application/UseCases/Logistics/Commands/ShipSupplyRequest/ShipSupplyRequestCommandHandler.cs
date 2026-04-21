using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;

public class ShipSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ShipSupplyRequestCommand, ShipSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<ShipSupplyRequestResponse> Handle(ShipSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanShip(sr.SourceStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho nguồn ngưng hoạt động hoặc đã đóng. Không thể xuất hàng cho yêu cầu tiếp tế.");

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.TransferOutAsync(
                sr.SourceDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Shipping", "InTransit", null, request.UserId, cancellationToken);
        });

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Vật phẩm đang được vận chuyển",
            $"Yêu cầu tiếp tế số {sr.Id}: hàng đã xuất kho và đang vận chuyển đến kho của bạn.",
            "supply_shipped",
            cancellationToken);

        await operationalHubService.PushSupplyRequestUpdateAsync(
            new SupplyRequestRealtimeUpdate
            {
                RequestId = sr.Id,
                RequestingDepotId = sr.RequestingDepotId,
                SourceDepotId = sr.SourceDepotId,
                Action = "Shipped",
                SourceStatus = "Shipping",
                RequestingStatus = "InTransit"
            },
            cancellationToken);

        await operationalHubService.PushDepotInventoryUpdateAsync(sr.SourceDepotId, "SupplyRequestShip", cancellationToken);

        return new ShipSupplyRequestResponse { Message = $"Đã xuất hàng cho yêu cầu số {sr.Id}." };
    }
}
