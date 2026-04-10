using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

public class UploadExternalResolutionCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureExternalItemRepository externalItemRepository,
    IDepotInventoryRepository inventoryRepository,
    ISystemFundRepository systemFundRepository,
    IDepotFundDrainService depotFundDrainService,
    IUnitOfWork unitOfWork,
    ILogger<UploadExternalResolutionCommandHandler> logger)
    : IRequestHandler<UploadExternalResolutionCommand, UploadExternalResolutionResponse>
{
    public async Task<UploadExternalResolutionResponse> Handle(
        UploadExternalResolutionCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "UploadExternalResolution | ManagerUserId={By}",
            request.ManagerUserId);

        // 1. Resolve depot từ manager token
        var depotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.ManagerUserId, cancellationToken)
            ?? throw new NotFoundException("Bạn hiện không phụ trách kho nào.");

        // 2. Tải kho
        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        // 3. Phải ở trạng thái Unavailable
        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Chỉ cho phép xử lý bên ngoài khi kho đang Unavailable.");

        // 4. Guard: không phải kho duy nhất còn hoạt động
        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(depotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

        // 5. Phải có phiên đóng kho đang chờ với hình thức ExternalResolution
        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(depotId, cancellationToken)
            ?? throw new ConflictException(
                "Kho chưa được đánh dấu xử lý bên ngoài. Admin cần gọi POST /{id}/close/mark-external trước.");

        if (existingClosure.ResolutionType != CloseResolutionType.ExternalResolution)
            throw new ConflictException(
                "Phiên đóng kho hiện tại không phải hình thức xử lý bên ngoài. Không thể thực hiện thao tác này.");

        // 6. Validate JSON items
        var items = request.Items;
        if (items == null || items.Count == 0)
            throw new BadRequestException("Danh sách hàng tồn kho rỗng. Vui lòng cung cấp ít nhất một dòng.");

        var invalidRows = items.Where(i => string.IsNullOrWhiteSpace(i.HandlingMethod)).ToList();
        if (invalidRows.Count > 0)
            throw new BadRequestException(
                $"Các dòng sau thiếu Hình thức xử lý: {string.Join(", ", invalidRows.Select(i => i.RowNumber))}.");

        var closureRecord = existingClosure;
        var now = DateTime.UtcNow;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 7a. Tạo DepotClosureExternalItem records (audit trail, per-item ImageUrl)
            var externalItems = items.Select(p => new CreateClosureExternalItemDto(
                DepotId:        depotId,
                ClosureId:      closureRecord.Id,
                ItemName:       p.ItemName,
                CategoryName:   p.CategoryName,
                ItemType:       p.ItemType,
                Unit:           p.Unit,
                Quantity:       p.Quantity,
                UnitPrice:      p.UnitPrice,
                TotalPrice:     p.TotalPrice,
                HandlingMethod: p.HandlingMethod,
                Recipient:      p.Recipient,
                Note:           p.Note,
                ImageUrl:       p.ImageUrl,
                ProcessedBy:    request.ManagerUserId,
                ProcessedAt:    now
            ));
            await externalItemRepository.CreateBulkAsync(externalItems, cancellationToken);

            // 7b. Zero-out toàn bộ inventory
            await inventoryRepository.ZeroOutForClosureAsync(
                depotId: depotId,
                closureId: closureRecord.Id,
                performedBy: request.ManagerUserId,
                note: "Xử lý bên ngoài hệ thống (JSON upload)",
                cancellationToken: cancellationToken);

            // 7c. Tính tiền thanh lý (Sold) → cộng vào quỹ hệ thống
            var soldRevenue = items
                .Where(p => p.HandlingMethod.Equals("Sold", StringComparison.OrdinalIgnoreCase)
                         && p.TotalPrice.HasValue && p.TotalPrice.Value > 0)
                .Sum(p => p.TotalPrice!.Value);

            if (soldRevenue > 0)
            {
                var systemFund = await systemFundRepository.GetOrCreateAsync(cancellationToken);
                systemFund.Credit(soldRevenue);
                await systemFundRepository.UpdateAsync(systemFund, cancellationToken);

                await systemFundRepository.CreateTransactionAsync(new SystemFundTransactionModel
                {
                    SystemFundId    = systemFund.Id,
                    TransactionType = SystemFundTransactionType.LiquidationRevenue,
                    Amount          = soldRevenue,
                    ReferenceType   = "DepotClosure",
                    ReferenceId     = closureRecord.Id,
                    Note            = $"Tiền thanh lý tài sản khi đóng kho #{depotId} — {soldRevenue:N0} VNĐ",
                    CreatedBy       = request.ManagerUserId,
                    CreatedAt       = now
                }, cancellationToken);

                logger.LogInformation(
                    "UploadExternalResolution | Sold revenue={Revenue} credited to SystemFund | DepotId={DepotId} ClosureId={ClosureId}",
                    soldRevenue, depotId, closureRecord.Id);
            }

            // 7d. Drain quỹ kho (balance > 0) về quỹ hệ thống
            await depotFundDrainService.DrainAllToSystemFundAsync(depotId, closureRecord.Id, request.ManagerUserId, cancellationToken);

            var actualConsumables = items.Where(i => i.ItemType == "Consumable").Sum(i => i.Quantity);
            var actualReusables = items.Where(i => i.ItemType == "Reusable").Sum(i => i.Quantity);
            closureRecord.RecordActualInventory(actualConsumables, actualReusables);

            // 7e. Đóng kho: set status=Closed, utilization=0, unassign manager trong domain
            depot.CompleteClosing();

            // 7f. Hoàn tất closure record: set status=Completed
            closureRecord.Complete(now);

            // Persist depot (status=Closed, utilization=0) và closure (status=Completed).
            // DepotRepository.UpdateAsync tự detect transition → Closed và set UnassignedAt
            // cho tất cả manager đang active trong cùng 1 call — không cần gọi UnassignManagerAsync riêng.
            await depotRepository.UpdateAsync(depot, cancellationToken);
            await closureRepository.UpdateAsync(closureRecord, cancellationToken);
            
            // Persist các thay đổi vào DB trong transaction hiện tại
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "UploadExternalResolution completed — depot CLOSED | DepotId={DepotId} Items={Count} ClosureId={ClosureId}",
            depotId, items.Count, closureRecord.Id);

        var (_, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(depotId, cancellationToken);

        return new UploadExternalResolutionResponse
        {
            DepotId            = depotId,
            DepotName          = depot.Name,
            ClosureId          = closureRecord.Id,
            ProcessedItemCount = items.Count,
            SoldRevenue        = items
                .Where(p => p.HandlingMethod.Equals("Sold", StringComparison.OrdinalIgnoreCase)
                         && p.TotalPrice.HasValue && p.TotalPrice.Value > 0)
                .Sum(p => p.TotalPrice!.Value),
            SnapshotConsumableUnits = closureRecord.SnapshotConsumableUnits,
            SnapshotReusableUnits   = closureRecord.SnapshotReusableUnits,
            ReusableItemsSkipped    = reusableInUse,
            Message = $"Đã ghi nhận {items.Count} dòng xử lý bên ngoài, xóa toàn bộ tồn kho và đóng kho thành công."
        };
    }
}
