using FluentValidation;
using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;

// ── Command ───────────────────────────────────────────────────────────────────
public class RejectSupplyRequestCommand : IRequest<RejectSupplyRequestResponse>
{
    public int SupplyRequestId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ── Response ──────────────────────────────────────────────────────────────────
public class RejectSupplyRequestResponse
{
    public string Message { get; set; } = string.Empty;
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class RejectSupplyRequestCommandValidator : AbstractValidator<RejectSupplyRequestCommand>
{
    public RejectSupplyRequestCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng cung cấp lý do từ chối.");
    }
}

// ── Request DTO (body) ────────────────────────────────────────────────────────
public class RejectSupplyRequestRequest
{
    public string Reason { get; set; } = string.Empty;
}

// ── Handler ───────────────────────────────────────────────────────────────────
public class RejectSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<RejectSupplyRequestCommand, RejectSupplyRequestResponse>
{
    public async Task<RejectSupplyRequestResponse> Handle(RejectSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        if (sr.SourceStatus != "Pending")
            throw new BadRequestException($"Yêu cầu #{sr.Id} không ở trạng thái chờ duyệt (hiện tại: {sr.SourceStatus}).");

        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new BadRequestException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Rejected", "Rejected", request.Reason, cancellationToken);

        // Notify requesting manager — kèm lý do từ chối
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Yêu cầu tiếp tế bị từ chối",
            $"Yêu cầu #{sr.Id} đã bị từ chối. Lý do: {request.Reason}",
            cancellationToken);

        return new RejectSupplyRequestResponse { Message = $"Đã từ chối yêu cầu #{sr.Id}." };
    }
}
