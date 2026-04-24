using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;

public class AcceptSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AcceptSupplyRequestCommand, AcceptSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<AcceptSupplyRequestResponse> Handle(AcceptSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanAccept(sr.SourceStatus, sr.RequestingStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho nguồn trong yêu cầu này.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho nguồn ngừng hoạt động hoặc đã đóng. Không thể chấp nhận yêu cầu tiếp tế.");

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.ReserveItemsAsync(
                sr.SourceDepotId,
                sr.Items,
                sr.Id,
                request.UserId,
                cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(
                sr.Id,
                nameof(SourceDepotStatus.Accepted),
                nameof(RequestingDepotStatus.Approved),
                null,
                request.UserId,
                cancellationToken);
        });

        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "Yêu cầu tiếp tế được chấp nhận",
            $"Yêu cầu tiếp tế số {sr.Id} đã được kho nguồn chấp nhận.",
            "supply_accepted",
            cancellationToken);

        await operationalHubService.PushSupplyRequestUpdateAsync(
            new SupplyRequestRealtimeUpdate
            {
                RequestId = sr.Id,
                RequestingDepotId = sr.RequestingDepotId,
                SourceDepotId = sr.SourceDepotId,
                Action = "Accepted",
                SourceStatus = nameof(SourceDepotStatus.Accepted),
                RequestingStatus = nameof(RequestingDepotStatus.Approved)
            },
            cancellationToken);

        await operationalHubService.PushDepotInventoryUpdateAsync(
            sr.SourceDepotId,
            "SupplyRequestAccept",
            cancellationToken);

        return new AcceptSupplyRequestResponse
        {
            Message = $"Đã chấp nhận yêu cầu số {sr.Id}."
        };
    }
}
