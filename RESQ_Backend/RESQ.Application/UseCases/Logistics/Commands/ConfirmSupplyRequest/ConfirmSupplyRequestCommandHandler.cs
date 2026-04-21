using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;

public class ConfirmSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmSupplyRequestCommand, ConfirmSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<ConfirmSupplyRequestResponse> Handle(ConfirmSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanConfirmReceived(sr.SourceStatus, sr.RequestingStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.RequestingDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho yêu cầu tiếp tế.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho của bạn ngưng hoạt động hoặc đã đóng. Không thể nhận hàng vào kho này.");

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.TransferInAsync(
                sr.RequestingDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Completed", "Received", null, request.UserId, cancellationToken);
        });

        await operationalHubService.PushSupplyRequestUpdateAsync(
            new SupplyRequestRealtimeUpdate
            {
                RequestId = sr.Id,
                RequestingDepotId = sr.RequestingDepotId,
                SourceDepotId = sr.SourceDepotId,
                Action = "Confirmed",
                SourceStatus = "Completed",
                RequestingStatus = "Received"
            },
            cancellationToken);

        await operationalHubService.PushDepotInventoryUpdateAsync(sr.RequestingDepotId, "SupplyRequestConfirm", cancellationToken);

        return new ConfirmSupplyRequestResponse { Message = $"Đã xác nhận nhận hàng yêu cầu #{sr.Id}. Quy trình hoàn tất." };
    }
}
