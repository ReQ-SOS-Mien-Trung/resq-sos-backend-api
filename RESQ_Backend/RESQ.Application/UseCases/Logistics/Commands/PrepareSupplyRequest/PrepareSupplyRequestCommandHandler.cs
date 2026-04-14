using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareSupplyRequest;

/// <summary>
/// Kho nguồn bắt đầu đóng gói / picking - chuyển trạng thái Accepted → Preparing.
/// RequestingDepotStatus giữ nguyên Approved.
/// </summary>
public class PrepareSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<PrepareSupplyRequestCommand, PrepareSupplyRequestResponse>
{
    public async Task<PrepareSupplyRequestResponse> Handle(PrepareSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanPrepare(sr.SourceStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho nguồn ngưng hoạt động hoặc đã đóng. Không thể chuẩn bị yêu cầu tiếp tế.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Preparing", "Approved", null, cancellationToken);

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Kho nguồn đang chuẩn bị hàng",
            $"Yêu cầu tiếp tế số {sr.Id}: kho nguồn đang đóng gói và chuẩn bị xuất hàng.",
            "supply_preparing",
            cancellationToken);

        return new PrepareSupplyRequestResponse { Message = $"Yêu cầu số {sr.Id} đã chuyển sang trạng thái đang chuẩn bị hàng." };
    }
}
