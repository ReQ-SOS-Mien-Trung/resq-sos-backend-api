using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareSupplyRequest;

/// <summary>
/// Kho nguồn bắt đầu đóng gói / picking — chuyển trạng thái Accepted → Preparing.
/// RequestingDepotStatus giữ nguyên Approved.
/// </summary>
public class PrepareSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<PrepareSupplyRequestCommand, PrepareSupplyRequestResponse>
{
    public async Task<PrepareSupplyRequestResponse> Handle(PrepareSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanPrepare(sr.SourceStatus);

        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Preparing", "Approved", null, cancellationToken);

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Kho nguồn đang chuẩn bị hàng",
            $"Yêu cầu #{sr.Id}: kho nguồn đang đóng gói và chuẩn bị xuất hàng.",
            "supply_preparing",
            cancellationToken);

        return new PrepareSupplyRequestResponse { Message = $"Yêu cầu #{sr.Id} đã chuyển sang trạng thái đang chuẩn bị hàng." };
    }
}
