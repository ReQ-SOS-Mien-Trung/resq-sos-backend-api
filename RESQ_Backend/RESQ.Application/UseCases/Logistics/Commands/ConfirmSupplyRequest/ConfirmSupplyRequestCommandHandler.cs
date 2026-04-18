using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;

/// <summary>
/// Manager kho yêu cầu xác nhận đã nhận hàng (TransferIn).
/// Chỉ cho phép khi kho nguồn đã hoàn tất giao hàng (SourceStatus = Completed).
/// Inventory kho yêu cầu tăng tương ứng → RequestingStatus chuyển sang Received.
/// </summary>
public class ConfirmSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmSupplyRequestCommand, ConfirmSupplyRequestResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    public async Task<ConfirmSupplyRequestResponse> Handle(ConfirmSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu cung cấp #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanConfirmReceived(sr.SourceStatus, sr.RequestingStatus);

        // Chỉ manager của kho yêu cầu (requesting depot) mới được confirm
        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != sr.RequestingDepotId)
            throw new SupplyRequestAccessDeniedException("Bạn không phải manager của kho yêu cầu tiếp tế.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho của bạn ngưng hoạt động hoặc đã đóng. Không thể nhận hàng vào kho này.");

        // Wrap trong transaction để đảm bảo TransferIn + UpdateStatus đồng bộ
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.TransferInAsync(
                sr.RequestingDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Completed", "Received", null, request.UserId, cancellationToken);
        });

        return new ConfirmSupplyRequestResponse { Message = $"Đã xác nhận nhận hàng yêu cầu #{sr.Id}. Quy trình hoàn tất." };
    }
}
