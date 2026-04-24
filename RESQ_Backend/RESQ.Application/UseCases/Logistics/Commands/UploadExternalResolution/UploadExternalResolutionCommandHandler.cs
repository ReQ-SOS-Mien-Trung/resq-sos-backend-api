using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
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
        {
            throw new ConflictException("Không thể đóng kho duy nhất còn đang hoạt động trong hệ thống.");
        }

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
        {
            throw new BadRequestException("Danh sách hàng tồn kho rỗng. Vui lòng cung cấp ít nhất một dòng.");
        }

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
            var actualInventoryItems = await depotRepository.GetLotDetailedInventoryForClosureAsync(depotId, cancellationToken);
            ValidateUploadedItemsAgainstCurrentInventory(items, actualInventoryItems);

            var externalItems = items.Select(p => new CreateClosureExternalItemDto(
                DepotId: depotId,
                ClosureId: closureRecord.Id,
                ItemModelId: p.ItemModelId,
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

            var actualConsumables = actualInventoryItems
                .Where(i => string.Equals(i.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Quantity);
            var actualReusables = actualInventoryItems
                .Where(i => string.Equals(i.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Quantity);
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

        await operationalHubService.PushDepotInventoryUpdateAsync(
            depotId,
            "ExternalResolutionUploaded",
            cancellationToken);

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
            ClosureStatus = closureRecord.Status.ToString(),
            ResolutionType = CloseResolutionType.ExternalResolution.ToString(),
            Message = $"Đã ghi nhận {items.Count} dòng xử lý bên ngoài và xóa toàn bộ tồn kho còn lại. Kho vẫn giữ trạng thái Closing, chờ xác nhận đóng kho."
        };
    }

    private static void ValidateUploadedItemsAgainstCurrentInventory(
        IReadOnlyCollection<ExternalResolutionItemDto> uploadedItems,
        IReadOnlyCollection<ClosureInventoryLotItemDto> actualItems)
    {
        if (actualItems.Count == 0)
        {
            throw new ConflictException(
                "Kho hiện không còn hàng tồn để xử lý bên ngoài. Vui lòng tải lại template mới nhất.");
        }

        var invalidIdentityMessages = uploadedItems
            .Select(GetIdentityValidationMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        if (invalidIdentityMessages.Count > 0)
        {
            throw new BadRequestException(
                "File xử lý bên ngoài thiếu hoặc sai khóa kỹ thuật. Vui lòng tải lại template mới nhất. Chi tiết: "
                + string.Join(" | ", invalidIdentityMessages));
        }

        var duplicateUploadKeys = uploadedItems
            .GroupBy(item => BuildInventoryRowKey(item.ItemModelId!.Value, item.ItemType, item.LotId))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(item => item.RowNumber).OrderBy(x => x)))
            .ToList();

        if (duplicateUploadKeys.Count > 0)
        {
            throw new BadRequestException(
                $"File upload có dòng trùng khóa kỹ thuật: {string.Join(" | ", duplicateUploadKeys)}.");
        }

        var actualLookup = actualItems.ToDictionary(
            item => BuildInventoryRowKey(item.ItemModelId, item.ItemType, item.LotId),
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var uploadedLookup = uploadedItems.ToDictionary(
            item => BuildInventoryRowKey(item.ItemModelId!.Value, item.ItemType, item.LotId),
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var issues = new List<string>();

        foreach (var uploaded in uploadedItems.OrderBy(item => item.RowNumber))
        {
            var key = BuildInventoryRowKey(uploaded.ItemModelId!.Value, uploaded.ItemType, uploaded.LotId);
            if (!actualLookup.TryGetValue(key, out var actual))
            {
                issues.Add(
                    $"dòng {uploaded.RowNumber}: không còn tồn thực tế tương ứng với ItemModelId={uploaded.ItemModelId}, LotId={(uploaded.LotId?.ToString() ?? "null")} ({uploaded.ItemName}).");
                continue;
            }

            if (!MatchesPreFilledInventoryData(uploaded, actual))
            {
                issues.Add(DescribePreFilledMismatch(uploaded, actual));
            }
        }

        var missingActualRows = actualItems
            .Where(item => !uploadedLookup.ContainsKey(BuildInventoryRowKey(item.ItemModelId, item.ItemType, item.LotId)))
            .Take(5)
            .Select(item => $"{item.ItemName} (ItemModelId={item.ItemModelId}, LotId={item.LotId?.ToString() ?? "null"})")
            .ToList();

        if (missingActualRows.Count > 0)
        {
            issues.Add($"file upload đang thiếu dòng tồn thực tế, ví dụ: {string.Join("; ", missingActualRows)}.");
        }

        if (issues.Count > 0)
        {
            throw new ConflictException(
                $"File xử lý bên ngoài không còn khớp với tồn thực tế của kho. Vui lòng tải lại template mới nhất. Chi tiết: {string.Join(" | ", issues.Take(8))}");
        }
    }

    private static string? GetIdentityValidationMessage(ExternalResolutionItemDto item)
    {
        var reasons = new List<string>();

        if (!item.ItemModelId.HasValue || item.ItemModelId.Value <= 0)
        {
            reasons.Add("thiếu hoặc sai ItemModelId");
        }

        if (!string.Equals(item.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("ItemType không hợp lệ");
        }
        else if (string.Equals(item.ItemType, "Consumable", StringComparison.OrdinalIgnoreCase) && !item.LotId.HasValue)
        {
            reasons.Add("đồ tiêu thụ bắt buộc phải có LotId");
        }
        else if (string.Equals(item.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase) && item.LotId.HasValue)
        {
            reasons.Add("đồ tái sử dụng không được có LotId");
        }

        return reasons.Count == 0
            ? null
            : $"dòng {item.RowNumber}: {string.Join(", ", reasons)}";
    }

    private static bool MatchesPreFilledInventoryData(
        ExternalResolutionItemDto uploaded,
        ClosureInventoryLotItemDto actual)
    {
        return string.Equals(Normalize(uploaded.ItemName), Normalize(actual.ItemName), StringComparison.OrdinalIgnoreCase)
               && string.Equals(Normalize(uploaded.CategoryName), Normalize(actual.CategoryName), StringComparison.OrdinalIgnoreCase)
               && string.Equals(Normalize(uploaded.TargetGroup), Normalize(actual.TargetGroup), StringComparison.OrdinalIgnoreCase)
               && string.Equals(Normalize(uploaded.ItemType), Normalize(actual.ItemType), StringComparison.OrdinalIgnoreCase)
               && string.Equals(Normalize(uploaded.Unit), Normalize(actual.Unit), StringComparison.OrdinalIgnoreCase)
               && uploaded.Quantity == actual.Quantity
               && SameDate(uploaded.ReceivedDate, actual.ReceivedDate)
               && SameDate(uploaded.ExpiredDate, actual.ExpiredDate);
    }

    private static string DescribePreFilledMismatch(
        ExternalResolutionItemDto uploaded,
        ClosureInventoryLotItemDto actual)
    {
        var differences = new List<string>();

        if (!string.Equals(Normalize(uploaded.ItemName), Normalize(actual.ItemName), StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"Tên vật phẩm hiện tại là '{actual.ItemName}'");
        }

        if (!string.Equals(Normalize(uploaded.CategoryName), Normalize(actual.CategoryName), StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"Danh mục hiện tại là '{actual.CategoryName}'");
        }

        if (!string.Equals(Normalize(uploaded.TargetGroup), Normalize(actual.TargetGroup), StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"Nhóm đối tượng hiện tại là '{actual.TargetGroup}'");
        }

        if (!string.Equals(Normalize(uploaded.ItemType), Normalize(actual.ItemType), StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"Loại vật phẩm hiện tại là '{actual.ItemType}'");
        }

        if (!string.Equals(Normalize(uploaded.Unit), Normalize(actual.Unit), StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"Đơn vị hiện tại là '{actual.Unit}'");
        }

        if (uploaded.Quantity != actual.Quantity)
        {
            differences.Add($"Số lượng hiện tại là {actual.Quantity}, file gửi {uploaded.Quantity}");
        }

        if (!SameDate(uploaded.ReceivedDate, actual.ReceivedDate))
        {
            differences.Add($"Ngày nhập hiện tại là {FormatNullableDate(actual.ReceivedDate)}, file gửi {FormatNullableDate(uploaded.ReceivedDate)}");
        }

        if (!SameDate(uploaded.ExpiredDate, actual.ExpiredDate))
        {
            differences.Add($"Hạn sử dụng hiện tại là {FormatNullableDate(actual.ExpiredDate)}, file gửi {FormatNullableDate(uploaded.ExpiredDate)}");
        }

        if (differences.Count == 0)
        {
            differences.Add("dữ liệu tiền điền đã thay đổi so với tồn thực tế");
        }

        return $"dòng {uploaded.RowNumber}: {string.Join("; ", differences)}.";
    }

    private static string BuildInventoryRowKey(int itemModelId, string itemType, int? lotId)
        => $"{itemModelId}:{itemType.Trim().ToUpperInvariant()}:{(lotId.HasValue ? lotId.Value.ToString() : "NULL")}";

    private static bool SameDate(DateTime? left, DateTime? right)
        => left?.Date == right?.Date;

    private static string FormatNullableDate(DateTime? value)
        => value.HasValue ? value.Value.ToString("dd/MM/yyyy") : "trống";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
