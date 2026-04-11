using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

public class UploadExternalResolutionCommandHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureExternalItemRepository externalItemRepository,
    IDepotInventoryRepository inventoryRepository,
    IDepotFundRepository depotFundRepo,
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

        var depotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(request.ManagerUserId, cancellationToken)
            ?? throw new NotFoundException("B?n hi?n không ph? trách kho nào.");

        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm th?y kho c?u tr?.");

        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException(
                $"Kho dang ? tr?ng thái '{depot.Status}'. Ch? cho phép x? lý bên ngoài khi kho dang Unavailable.");

        var activeCount = await depotRepository.GetActiveDepotCountExcludingAsync(depotId, cancellationToken);
        if (activeCount == 0)
            throw new ConflictException("Không th? dóng kho duy nh?t còn dang ho?t d?ng trong h? th?ng.");

        var existingClosure = await closureRepository.GetActiveClosureByDepotIdAsync(depotId, cancellationToken)
            ?? throw new ConflictException(
                "Kho chua du?c dánh d?u x? lý bên ngoài. Admin c?n g?i POST /{id}/close/mark-external tru?c.");

        if (existingClosure.ResolutionType != CloseResolutionType.ExternalResolution)
            throw new ConflictException(
                "Phiên dóng kho hi?n t?i không ph?i hình th?c x? lý bên ngoài. Không th? th?c hi?n thao tác này.");

        var items = request.Items;
        if (items == null || items.Count == 0)
            throw new BadRequestException("Danh sách hàng t?n kho r?ng. Vui lòng cung c?p ít nh?t m?t dòng.");

        var invalidRows = items.Where(i => string.IsNullOrWhiteSpace(i.HandlingMethod)).ToList();
        if (invalidRows.Count > 0)
            throw new BadRequestException(
                $"Các dòng sau thi?u Hình th?c x? lý: {string.Join(", ", invalidRows.Select(i => i.RowNumber))}.");

        var invalidHandlingMethodRows = items
            .Where(i => !string.IsNullOrWhiteSpace(i.HandlingMethod)
                     && !ExternalDispositionMetadata.Parse(i.HandlingMethod).HasValue)
            .ToList();
        if (invalidHandlingMethodRows.Count > 0)
            throw new BadRequestException(
                $"Các dòng sau có Hình th?c x? lý không h?p l?: {string.Join(", ", invalidHandlingMethodRows.Select(i => i.RowNumber))}.");

        var otherRowsMissingNote = items
            .Where(i => ExternalDispositionMetadata.Parse(i.HandlingMethod) == ExternalDispositionType.Other
                     && string.IsNullOrWhiteSpace(i.Note))
            .ToList();
        if (otherRowsMissingNote.Count > 0)
            throw new BadRequestException(
                $"Các dòng sau ch?n HandlingMethod = Other nhung thi?u Ghi chú: {string.Join(", ", otherRowsMissingNote.Select(i => i.RowNumber))}.");

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
                note: "X? lý bên ngoài h? th?ng (JSON upload)",
                cancellationToken: cancellationToken);

                        if (liquidationRevenue > 0)
            {
                var depotFund = await depotFundRepo.GetOrCreateByDepotAndSourceAsync(
                    depotId, FundSourceType.SystemFund, null, cancellationToken);
                
                depotFund.Credit(liquidationRevenue);
                await depotFundRepo.UpdateAsync(depotFund, cancellationToken);

                await depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                {
                    DepotFundId = depotFund.Id,
                    TransactionType = DepotFundTransactionType.LiquidationRevenue,
                    Amount = liquidationRevenue,
                    ReferenceType = "DepotClosure",
                    ReferenceId = closureRecord.Id,
                    Note = $"Ti?n thanh lý tài s?n khi dóng kho #{depotId} - {liquidationRevenue:N0} VNÐ",
                    CreatedBy = request.ManagerUserId,
                    CreatedAt = now
                }, cancellationToken);

                logger.LogInformation(
                    "UploadExternalResolution | Liquidation revenue={Revenue} credited to DepotFund (SystemFund source) | DepotId={DepotId} ClosureId={ClosureId}",
                    liquidationRevenue, depotId, closureRecord.Id);
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
            depotId, items.Count, closureRecord.Id);

        var (_, reusableInUse) = await depotRepository.GetReusableItemCountsAsync(depotId, cancellationToken);

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
            Message = $"Ðã ghi nh?n {items.Count} dòng x? lý bên ngoài và xóa toàn b? t?n kho. Kho v?n gi? tr?ng thái Unavailable, ch? admin xác nh?n dóng kho."
        };
    }
}



