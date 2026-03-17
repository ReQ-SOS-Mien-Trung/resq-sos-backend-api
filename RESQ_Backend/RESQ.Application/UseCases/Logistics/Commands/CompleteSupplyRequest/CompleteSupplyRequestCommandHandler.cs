using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;

/// <summary>
/// Kho nguồn xác nhận đã hoàn tất giao hàng — chuyển SourceStatus: Shipped → Completed.
/// RequestingDepotStatus vẫn giữ InTransit cho đến khi kho yêu cầu xác nhận nhận hàng.
/// </summary>
public class CompleteSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<CompleteSupplyRequestCommand, CompleteSupplyRequestResponse>
{
    public async Task<CompleteSupplyRequestResponse> Handle(CompleteSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        if (sr.SourceStatus != "Shipped")
            throw new BadRequestException($"Yêu cầu #{sr.Id} không ở trạng thái đã vận chuyển (hiện tại: {sr.SourceStatus}).");

        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new BadRequestException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Completed", "InTransit", null, cancellationToken);

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Kho nguồn đã hoàn tất giao hàng",
            $"Yêu cầu #{sr.Id}: kho nguồn xác nhận đã giao hàng. Vui lòng kiểm tra và xác nhận nhận hàng.",
            cancellationToken);

        return new CompleteSupplyRequestResponse { Message = $"Đã xác nhận hoàn tất giao hàng cho yêu cầu #{sr.Id}." };
    }
}
