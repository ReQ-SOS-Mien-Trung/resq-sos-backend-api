using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;

/// <summary>
/// Kho nguồn xác nhận đã hoàn tất giao hàng - chuyển SourceStatus: Shipping → Completed.
/// RequestingDepotStatus vẫn giữ InTransit - đợi kho yêu cầu xác nhận nhận hàng sau.
/// </summary>
public class CompleteSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<CompleteSupplyRequestCommand, CompleteSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    public async Task<CompleteSupplyRequestResponse> Handle(CompleteSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanComplete(sr.SourceStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho nguồn ngưng hoạt động hoặc đã đóng. Không thể xác nhận hoàn tất giao hàng.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Completed", "InTransit", null, request.UserId, cancellationToken);

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Kho nguồn đã hoàn tất giao hàng",
            $"Yêu cầu tiếp tế số {sr.Id}: kho nguồn xác nhận đã giao hàng. Vui lòng kiểm tra và xác nhận nhận hàng.",
            "supply_completed",
            cancellationToken);

        return new CompleteSupplyRequestResponse { Message = $"Đã xác nhận hoàn tất giao hàng cho yêu cầu số {sr.Id}." };
    }
}
