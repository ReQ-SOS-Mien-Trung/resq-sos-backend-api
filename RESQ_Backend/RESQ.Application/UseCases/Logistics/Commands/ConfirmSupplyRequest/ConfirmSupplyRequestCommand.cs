using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;

// ── Command ───────────────────────────────────────────────────────────────────
public record ConfirmSupplyRequestCommand(int SupplyRequestId, Guid UserId) : IRequest<ConfirmSupplyRequestResponse>;

// ── Response ──────────────────────────────────────────────────────────────────
public class ConfirmSupplyRequestResponse
{
    public string Message { get; set; } = string.Empty;
}

// ── Handler ───────────────────────────────────────────────────────────────────
/// <summary>
/// Manager kho yêu cầu xác nhận đã nhận hàng (TransferIn).
/// Inventory kho yêu cầu tăng tương ứng → hoàn tất quy trình.
/// </summary>
public class ConfirmSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<ConfirmSupplyRequestCommand, ConfirmSupplyRequestResponse>
{
    public async Task<ConfirmSupplyRequestResponse> Handle(ConfirmSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        if (sr.RequestingStatus != "InTransit")
            throw new BadRequestException($"Yêu cầu #{sr.Id} không ở trạng thái đang vận chuyển (hiện tại: {sr.RequestingStatus}).");

        // Chỉ manager của kho yêu cầu (requesting depot) mới được confirm
        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.RequestingDepotId)
            throw new BadRequestException("Bạn không phải manager của kho yêu cầu tiếp tế.");

        // Nhập kho — tăng tồn kho kho yêu cầu
        await supplyRequestRepository.TransferInAsync(
            sr.RequestingDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Completed", "Received", null, cancellationToken);

        return new ConfirmSupplyRequestResponse { Message = $"Đã xác nhận nhận hàng yêu cầu #{sr.Id}. Quy trình hoàn tất." };
    }
}
