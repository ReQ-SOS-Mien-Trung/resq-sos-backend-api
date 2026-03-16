using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;

public class AcceptSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<AcceptSupplyRequestCommand, AcceptSupplyRequestResponse>
{
    public async Task<AcceptSupplyRequestResponse> Handle(AcceptSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        if (sr.SourceStatus != "Pending")
            throw new BadRequestException($"Yêu cầu #{sr.Id} không ở trạng thái chờ duyệt (hiện tại: {sr.SourceStatus}).");

        // Chỉ manager của kho nguồn mới được accept
        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new BadRequestException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Accepted", "Approved", null, cancellationToken);

        // Notify requesting manager
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Yêu cầu tiếp tế được chấp nhận",
            $"Yêu cầu #{sr.Id} đã được kho nguồn chấp nhận và đang chuẩn bị hàng.",
            cancellationToken);

        return new AcceptSupplyRequestResponse { Message = $"Đã chấp nhận yêu cầu #{sr.Id}." };
    }
}
