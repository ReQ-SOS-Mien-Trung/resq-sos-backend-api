using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

public class UploadExternalResolutionCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureExternalItemRepository externalItemRepository,
    IDepotInventoryRepository inventoryRepository,
    IDepotFundRepository depotFundRepo,
    IOperationalHubService operationalHubService,
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

        var depotId = await managerDepotAccessService.ResolveAccessibleDepotIdAsync(
            request.ManagerUserId,
            request.DepotId,
            cancellationToken)
            ?? throw new NotFoundException("Bạn hiện không phụ trách kho nào.");

        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (depot.Status != DepotStatus.Closing)
        {
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Chỉ cho phép xử lý bên ngoài khi kho đang Closing.");
        }

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(depotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");

        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(depotId, cancellationToken)
            ?? throw new ConflictException(
                "Kho chưa có phiên xử lý bên ngoài hợp lệ. Admin cần gọi POST /{id}/closed trước, sau đó gọi POST /{id}/close/mark-external.");

        if (existingClosure.ResolutionType != CloseResolutionType.ExternalResolution)
        {
            throw new ConflictException(
                "Phiên đóng kho hiện tại không phải hình thức xử lý bên ngoài. Không thể thực hiện thao tác này.");
        }

        var items = request.Items;
        if (items == null || items.Count == 0)
            throw new BadRequestException("Danh sách hàng tồn kho rỗng. Vui lòng cung cấp ít nhất một dòng.");

        var invalidRows = items.Where(i => string.IsNullOrWhiteSpace(i.HandlingMethod)).ToList();
        if (invalidRows.Count > 0)
        {
            throw new BadRequestException(
                $"Các dòng sau thiếu Hình thức xử lý: {string.Join(", ", invalidRows.Select(i => i.RowNumber))}.");
        }

        var invalidHandlingMethodRows = items
            .Where(i => !string.IsNullOrWhiteSpace(i.HandlingMethod)
                     && !ExternalDispositionMetadata.Parse(i.HandlingMethod).HasValue)
            .ToList();
        if (invalidHandlingMethodRows.Count > 0)
        {
            throw new BadRequestException(
                $"Các dòng sau có Hình thức xử lý không hợp lệ: {string.Join(", ", invalidHandlingMethodRows.Select(i => i.RowNumber))}.");
        }

        var otherRowsMissingNote = items
            .Where(i => ExternalDispositionMetadata.Parse(i.HandlingMethod) == ExternalDispositionType.Other
                     && string.IsNullOrWhiteSpace(i.Note))
            .ToList();
        if (otherRowsMissingNote.Count > 0)
        {
            throw new BadRequestException(
                $"Các dòng sau chọn HandlingMethod = Other nhưng thiếu Ghi chú: {string.Join(", ", otherRowsMissingNote.Select(i => i.RowNumber))}.");
        }

        var closureRecord = existingClosure;
        var now = DateTime.UtcNow;
        var liquidationRevenue = items
            .Where(p => ExternalDispositionMetadata.Parse(p.HandlingMethod) == ExternalDispositionType.Liquidated
                     && p.TotalPrice.HasValue && p.TotalPrice.Value > 0)
            .Sum(p => p.TotalPrice!.Value);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var externalItems = items.Select(p => new CreateClosureExternalItemDto(
                DepotId: depotId,
                ClosureId: closureRecord.Id,
                ItemName: p.ItemName,
                CategoryName: p.CategoryName,
                ItemType: p.ItemType,
                Unit: p.Unit,
                Quantity: p.Quantity,
                UnitPrice: p.UnitPrice,
                TotalPrice: p.TotalPrice,
                HandlingMethod: ExternalDispositionMetadata.Parse(p.HandlingMethod)!.Value.ToString(),
                Recipient: p.Recipient,
                Note: p.Note,
                ImageUrl: p.ImageUrl,
                ProcessedBy: request.ManagerUserId,
                ProcessedAt: now
            ));
            await externalItemRepository.CreateBulkAsync(externalItems, cancellationToken);

            await inventoryRepository.ZeroOutForClosureAsync(
                depotId: depotId,
                closureId: closureRecord.Id,
                performedBy: request.ManagerUserId,
                note: "Xử lý bên ngoài hệ thống (JSON upload)",
                cancellationToken: cancellationToken);

            if (liquidationRevenue > 0)
            {
                var depotFund = await depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    depotId,
                    FundSourceType.SystemFund,
                    null,
                    cancellationToken);

                depotFund.Credit(liquidationRevenue);
                await depotFundRepo.UpdateAsync(depotFund, cancellationToken);

                await depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                {
                    DepotFundId = depotFund.Id,
                    TransactionType = DepotFundTransactionType.LiquidationRevenue,
                    Amount = liquidationRevenue,
                    ReferenceType = "DepotClosure",
                    ReferenceId = closureRecord.Id,
                    Note = $"Tiền thanh lý tài sản khi đóng kho #{depotId} - {liquidationRevenue:N0} VNĐ",
                    CreatedBy = request.ManagerUserId,
                    CreatedAt = now
                }, cancellationToken);

                logger.LogInformation(
                    "UploadExternalResolution | LiquidationRevenue={Revenue} DepotId={DepotId} ClosureId={ClosureId}",
                    liquidationRevenue,
                    depotId,
                    closureRecord.Id);
            }

            var actualConsumables = items.Where(i => i.ItemType == "Consumable").Sum(i => i.Quantity);
            var actualReusables = items.Where(i => i.ItemType == "Reusable").Sum(i => i.Quantity);
            closureRecord.RecordActualInventory(actualConsumables, actualReusables);
            closureRecord.Complete(now);

            await closureRepository.UpdateAsync(closureRecord, cancellationToken);
            await unitOfWork.SaveAsync();
        });

        logger.LogInformation(
            "UploadExternalResolution completed | DepotId={DepotId} Items={Count} ClosureId={ClosureId}",
            depotId,
            items.Count,
            closureRecord.Id);

        (_, var reusableInUse) = await depotRepository.GetReusableItemCountsAsync(depotId, cancellationToken);

        await operationalHubService.PushDepotClosureUpdateAsync(
            new DepotClosureRealtimeUpdate
            {
                SourceDepotId = depotId,
                ClosureId = closureRecord.Id,
                EntityType = "Closure",
                Action = "ExternalResolutionUploaded",
                Status = closureRecord.Status.ToString()
            },
            cancellationToken);

        await operationalHubService.PushDepotInventoryUpdateAsync(depotId, "ExternalResolutionUploaded", cancellationToken);

        return new UploadExternalResolutionResponse
        {
            DepotId = depotId,
            DepotName = depot.Name,
            ClosureId = closureRecord.Id,
            ProcessedItemCount = items.Count,
            SoldRevenue = liquidationRevenue,
            SnapshotConsumableUnits = closureRecord.SnapshotConsumableUnits,
            SnapshotReusableUnits = closureRecord.SnapshotReusableUnits,
            ReusableItemsSkipped = reusableInUse,
            Message = $"Đã ghi nhận {items.Count} dòng xử lý bên ngoài và xóa toàn bộ tồn kho. Kho vẫn giữ trạng thái Closing, chờ xác nhận đóng kho."
        };
    }
}
