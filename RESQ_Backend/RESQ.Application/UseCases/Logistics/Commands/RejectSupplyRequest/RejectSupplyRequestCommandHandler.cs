using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;

public class RejectSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<RejectSupplyRequestCommand, RejectSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    public async Task<RejectSupplyRequestResponse> Handle(RejectSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanReject(sr.SourceStatus, sr.RequestingStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Rejected", "Rejected", request.Reason, cancellationToken);

        // Notify requesting manager - kèm lý do từ chối
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Yêu cầu tiếp tế bị từ chối",
            $"Yêu cầu tiếp tế số {sr.Id} đã bị từ chối. Lý do: {request.Reason}",
            "supply_rejected",
            cancellationToken);

        return new RejectSupplyRequestResponse { Message = $"Đã từ chối yêu cầu số {sr.Id}." };
    }
}
