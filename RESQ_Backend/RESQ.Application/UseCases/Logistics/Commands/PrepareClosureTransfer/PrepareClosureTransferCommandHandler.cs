using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;

public class PrepareClosureTransferCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<PrepareClosureTransferCommandHandler> logger)
    : IRequestHandler<PrepareClosureTransferCommand, PrepareClosureTransferResponse>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<PrepareClosureTransferResponse> Handle(
        PrepareClosureTransferCommand request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        if (managerDepotId != transfer.SourceDepotId)
            throw new ForbiddenException("Bạn không phải manager của kho nguồn trong quá trình chuyển hàng này.");

        transfer.MarkPreparing(request.UserId, request.Note);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await transferRepository.UpdateAsync(transfer, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "ClosureTransfer preparing | TransferId={TransferId} By={UserId}",
            transfer.Id, request.UserId);

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = transfer.SourceDepotId,
                TargetDepotId = transfer.TargetDepotId,
                ClosureId = transfer.ClosureId,
                TransferId = transfer.Id,
                EntityType = "Transfer",
                Action = "Preparing",
                Status = transfer.Status
            },
            cancellationToken);

        return new PrepareClosureTransferResponse
        {
            TransferId = transfer.Id,
            TransferStatus = transfer.Status,
            Message = "Đã xác nhận chuẩn bị hàng. Tiến hành đóng gói và xuất kho khi sẵn sàng."
        };
    }
}
