using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;

public class AcceptSupplyRequestCommandHandler(
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AcceptSupplyRequestCommand, AcceptSupplyRequestResponse>
{
    public async Task<AcceptSupplyRequestResponse> Handle(AcceptSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanAccept(sr.SourceStatus, sr.RequestingStatus);

        // Chỉ manager của kho nguồn mới được accept
        var managerDepotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho nguồn đang trong quá trình đóng hoặc đã đóng. Không thể chấp nhận yêu cầu tiếp tế.");

        // Wrap trong transaction để đảm bảo Reserve + UpdateStatus đồng bộ
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.ReserveItemsAsync(
                sr.SourceDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Accepted", "Approved", null, cancellationToken);
        });

        // Notify requesting manager
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Yêu cầu tiếp tế được chấp nhận",
            $"Yêu cầu tiếp tế số {sr.Id} đã được kho nguồn chấp nhận và đang chuẩn bị hàng.",
            "supply_accepted",
            cancellationToken);

        return new AcceptSupplyRequestResponse { Message = $"Đã chấp nhận yêu cầu số {sr.Id}." };
    }
}
