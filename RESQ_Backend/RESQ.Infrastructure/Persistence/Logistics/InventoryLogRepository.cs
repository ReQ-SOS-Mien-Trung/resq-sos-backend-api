using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Common;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class InventoryLogRepository(IUnitOfWork unitOfWork) : IInventoryLogRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<InventoryLogModel>> GetInventoryLogsPagedAsync(
        int? depotId,
        int? itemModelId,
        List<string>? actionTypes,
        List<string>? sourceTypes,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<InventoryLog>()
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.ItemModel)
            .Include(x => x.ItemModel)
            .Include(x => x.SupplyInventoryLot)
            .Include(x => x.ReusableItem)
                .ThenInclude(x => x!.ItemModel)
            .Include(x => x.ReusableItem)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.PerformedByUser)
            .Include(x => x.VatInvoice)
            .AsQueryable();

        if (depotId.HasValue)
        {
            query = ApplyDepotFilter(query, depotId.Value);
        }

        if (itemModelId.HasValue)
        {
            query = query.Where(x => x.ItemModelId == itemModelId.Value
                || (x.SupplyInventory != null && x.SupplyInventory.ItemModelId == itemModelId.Value)
                || (x.ReusableItem != null && x.ReusableItem.ItemModelId == itemModelId.Value));
        }

        if (actionTypes != null && actionTypes.Count > 0)
        {
            var actionTypesLower = actionTypes.Select(a => a.ToLower()).ToList();
            query = query.Where(x => actionTypesLower.Contains(x.ActionType!.ToLower()));
        }

        if (sourceTypes != null && sourceTypes.Count > 0)
        {
            var sourceTypesLower = sourceTypes.Select(s => s.ToLower()).ToList();
            query = query.Where(x => sourceTypesLower.Contains(x.SourceType!.ToLower()));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Note != null && x.Note.ToLower().Contains(normalizedSearch))
                || (x.PerformedByUser != null
                    && (x.PerformedByUser.LastName + " " + x.PerformedByUser.FirstName).ToLower().Contains(normalizedSearch))
                || (x.VatInvoice != null && x.VatInvoice.SupplierName != null && x.VatInvoice.SupplierName.ToLower().Contains(normalizedSearch))
                || (x.VatInvoice != null && x.VatInvoice.InvoiceNumber != null && x.VatInvoice.InvoiceNumber.ToLower().Contains(normalizedSearch))
                || (x.VatInvoice != null && x.VatInvoice.InvoiceSerial != null && x.VatInvoice.InvoiceSerial.ToLower().Contains(normalizedSearch))
                || (x.VatInvoice != null && x.VatInvoice.SupplierTaxCode != null && x.VatInvoice.SupplierTaxCode.ToLower().Contains(normalizedSearch)));
        }

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var rawLogs = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var currentQuantityMap = await BuildCurrentQuantityMapAsync(rawLogs, cancellationToken);

        var logs = rawLogs.Select(x =>
        {
            var itemModel = ResolveItemModel(x);

            return new InventoryLogModel
            {
                Id = x.Id,
                DepotSupplyInventoryId = x.DepotSupplyInventoryId,
                SupplyInventoryLotId = x.SupplyInventoryLotId,
                ActionType = x.ActionType ?? string.Empty,
                QuantityChange = x.QuantityChange,
                SourceType = x.SourceType ?? string.Empty,
                SourceId = x.SourceId,
                Note = NormalizeMultilineText(x.Note),
                CreatedAt = x.CreatedAt,
                ReceivedDate = x.ReceivedDate,
                ExpiredDate = x.ExpiredDate,
                PerformedByName = x.PerformedByUser != null
                    ? $"{x.PerformedByUser.LastName} {x.PerformedByUser.FirstName}".Trim()
                    : string.Empty,
                DepotId = x.SupplyInventory?.DepotId ?? x.ReusableItem?.DepotId,
                DepotName = x.SupplyInventory?.Depot?.Name ?? x.ReusableItem?.Depot?.Name ?? string.Empty,
                ItemModelId = x.ItemModelId ?? x.SupplyInventory?.ItemModelId ?? x.ReusableItem?.ItemModelId,
                ItemModelName = itemModel?.Name ?? string.Empty,
                RemainingQuantity = TryGetRemainingQuantity(currentQuantityMap, x),
                SerialNumber = x.ReusableItem?.SerialNumber,
                LotId = x.SupplyInventoryLot?.Id,
                ReusableItemId = x.ReusableItemId,
                VatInvoiceId = x.VatInvoiceId,
                InvoiceSerial = x.VatInvoice?.InvoiceSerial,
                InvoiceNumber = x.VatInvoice?.InvoiceNumber,
                SupplierName = x.VatInvoice?.SupplierName,
                SupplierTaxCode = x.VatInvoice?.SupplierTaxCode,
                InvoiceDate = x.VatInvoice?.InvoiceDate,
                InvoiceTotalAmount = x.VatInvoice?.TotalAmount,
                InvoiceFileUrl = x.VatInvoice?.FileUrl
            };
        }).ToList();

        return new PagedResult<InventoryLogModel>(logs, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<InventoryTransactionDto>> GetTransactionHistoryAsync(
        int? depotId,
        int? itemModelId,
        List<string>? actionTypes,
        List<string>? sourceTypes,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<InventoryLog>()
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.Depot)
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.ItemModel)
                    .ThenInclude(x => x!.Category)
            .Include(x => x.SupplyInventory)
                .ThenInclude(x => x!.ItemModel)
                    .ThenInclude(x => x!.TargetGroups)
            .Include(x => x.ItemModel)
                .ThenInclude(x => x!.Category)
            .Include(x => x.ItemModel)
                .ThenInclude(x => x!.TargetGroups)
            .Include(x => x.SupplyInventoryLot)
            .Include(x => x.ReusableItem)
                .ThenInclude(x => x!.ItemModel)
                    .ThenInclude(x => x!.Category)
            .Include(x => x.ReusableItem)
                .ThenInclude(x => x!.ItemModel)
                    .ThenInclude(x => x!.TargetGroups)
            .Include(x => x.PerformedByUser)
            .Include(x => x.VatInvoice)
            .AsQueryable();

        if (depotId.HasValue)
        {
            query = ApplyDepotFilter(query, depotId.Value);
        }

        if (itemModelId.HasValue)
        {
            query = query.Where(x => x.ItemModelId == itemModelId.Value
                || (x.SupplyInventory != null && x.SupplyInventory.ItemModelId == itemModelId.Value)
                || (x.ReusableItem != null && x.ReusableItem.ItemModelId == itemModelId.Value));
        }

        if (actionTypes != null && actionTypes.Count > 0)
        {
            var actionTypesLower = actionTypes.Select(a => a.ToLower()).ToList();
            query = query.Where(x => actionTypesLower.Contains(x.ActionType!.ToLower()));
        }

        if (sourceTypes != null && sourceTypes.Count > 0)
        {
            var sourceTypesLower = sourceTypes.Select(s => s.ToLower()).ToList();
            query = query.Where(x => sourceTypesLower.Contains(x.SourceType!.ToLower()));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) <= toDate.Value);
        }

        var rawLogs = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var currentQuantityMap = await BuildCurrentQuantityMapAsync(rawLogs, cancellationToken);

        var groups = rawLogs
            .GroupBy(BuildTransactionGroupKey)
            .OrderByDescending(g => g.Min(x => x.CreatedAt))
            .ToList();

        var totalCount = groups.Count;

        var paginatedGroups = groups
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = paginatedGroups.Select(g =>
        {
            var firstItem = g.First();
            var createdAt = g.Min(x => x.CreatedAt);
            var transactionId = FormatReadableTransactionId(firstItem, createdAt);

            return new InventoryTransactionDto
            {
                TransactionId = transactionId,
                ActionType = firstItem.ActionType ?? string.Empty,
                SourceType = firstItem.SourceType ?? string.Empty,
                SourceId = firstItem.SourceId,
                SourceName = GetSourceName(firstItem.SourceType, firstItem.SourceId),
                PerformedByName = firstItem.PerformedByUser != null
                    ? $"{firstItem.PerformedByUser.LastName} {firstItem.PerformedByUser.FirstName}".Trim()
                    : string.Empty,
                Note = NormalizeMultilineText(firstItem.Note),
                CreatedAt = createdAt ?? DateTime.MinValue,
                VatInvoiceId = firstItem.VatInvoiceId,
                InvoiceSerial = firstItem.VatInvoice?.InvoiceSerial,
                InvoiceNumber = firstItem.VatInvoice?.InvoiceNumber,
                SupplierName = firstItem.VatInvoice?.SupplierName,
                SupplierTaxCode = firstItem.VatInvoice?.SupplierTaxCode,
                InvoiceDate = firstItem.VatInvoice?.InvoiceDate,
                InvoiceTotalAmount = firstItem.VatInvoice?.TotalAmount,
                InvoiceFileUrl = firstItem.VatInvoice?.FileUrl,
                Items = g.Select(item =>
                {
                    var itemModel = ResolveItemModel(item);
                    var resolvedItemModelId = item.ItemModelId
                                              ?? item.SupplyInventory?.ItemModelId
                                              ?? item.ReusableItem?.ItemModelId
                                              ?? 0;

                    return new InventoryTransactionItemDto
                    {
                        ItemId = resolvedItemModelId,
                        ItemModelId = resolvedItemModelId,
                        RemainingQuantity = TryGetRemainingQuantity(currentQuantityMap, item),
                        SupplyInventoryLotId = item.SupplyInventoryLotId,
                        LotId = item.SupplyInventoryLotId,
                        ReusableItemId = IsUnitLevelReusableAction(item) ? item.ReusableItemId : null,
                        SerialNumber = IsUnitLevelReusableAction(item) ? item.ReusableItem?.SerialNumber : null,
                        ItemName = itemModel?.Name ?? string.Empty,
                        QuantityChange = item.QuantityChange ?? 0,
                        FormattedQuantityChange = FormatQuantityChange(item.ActionType ?? string.Empty, item.QuantityChange ?? 0),
                        Unit = itemModel?.Unit ?? string.Empty,
                        ItemType = itemModel?.ItemType ?? string.Empty,
                        TargetGroup = TargetGroupTranslations.JoinAsVietnamese(
                            (itemModel?.TargetGroups
                             ?? Enumerable.Empty<RESQ.Infrastructure.Entities.Logistics.TargetGroup>())
                            .Select(tg => tg.Name)),
                        CategoryName = itemModel?.Category?.Name,
                        ReceivedDate = item.ReceivedDate,
                        ExpiredDate = item.ExpiredDate
                    };
                }).ToList()
            };
        }).ToList();

        return new PagedResult<InventoryTransactionDto>(result, totalCount, pageNumber, pageSize);
    }

    private string GetSourceName(string? sourceType, int? sourceId)
    {
        if (string.IsNullOrEmpty(sourceType) || !sourceId.HasValue)
        {
            return string.Empty;
        }

        return sourceType.ToLower() switch
        {
            "donation" => GetOrganizationName(sourceId.Value),
            "mission" => $"Mission #{sourceId.Value}",
            "missionactivity" => $"Hoạt động #{sourceId.Value}",
            "transfer" => $"Transfer #{sourceId.Value}",
            "depotclosure" => $"Phiên đóng kho #{sourceId.Value}",
            "adjustment" => "Điều chỉnh thủ công",
            _ => $"{sourceType} #{sourceId.Value}"
        };
    }

    private string GetOrganizationName(int organizationId)
    {
        var organization = _unitOfWork.Set<Organization>()
            .Where(o => o.Id == organizationId)
            .Select(o => o.Name)
            .FirstOrDefault();

        return organization ?? $"Organization #{organizationId}";
    }

    private static string FormatQuantityChange(string actionType, int quantityChange)
    {
        if (InventoryLogMetadataMappings.IsPositiveAction(actionType))
        {
            return $"+ {quantityChange}";
        }

        if (InventoryLogMetadataMappings.IsNegativeAction(actionType))
        {
            return $"- {Math.Abs(quantityChange)}";
        }

        return actionType.Equals(nameof(InventoryActionType.Adjust), StringComparison.OrdinalIgnoreCase)
            ? quantityChange >= 0 ? $"+ {quantityChange}" : $"- {Math.Abs(quantityChange)}"
            : quantityChange.ToString();
    }

    private static string? NormalizeMultilineText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.Replace("\\r\\n", "\n").Replace("\\n", "\n");
    }

    private async Task<Dictionary<(int DepotId, int ItemModelId), int>> BuildCurrentQuantityMapAsync(
        IEnumerable<InventoryLog> logs,
        CancellationToken cancellationToken)
    {
        var pairs = logs
            .Select(log =>
            {
                var depotId = ResolveDepotId(log);
                var itemModelId = log.ItemModelId
                                  ?? log.SupplyInventory?.ItemModelId
                                  ?? log.ReusableItem?.ItemModelId;
                return depotId.HasValue && itemModelId.HasValue
                    ? (DepotId: depotId.Value, ItemModelId: itemModelId.Value)
                    : ((int DepotId, int ItemModelId)?)null;
            })
            .Where(pair => pair.HasValue)
            .Select(pair => pair!.Value)
            .Distinct()
            .ToList();

        if (pairs.Count == 0)
        {
            return [];
        }

        var depotIds = pairs.Select(pair => pair.DepotId).Distinct().ToList();
        var itemModelIds = pairs.Select(pair => pair.ItemModelId).Distinct().ToList();

        return await _unitOfWork.Set<SupplyInventory>()
            .AsNoTracking()
            .Where(inventory => inventory.DepotId.HasValue
                && inventory.ItemModelId.HasValue
                && depotIds.Contains(inventory.DepotId.Value)
                && itemModelIds.Contains(inventory.ItemModelId.Value))
            .Select(inventory => new
            {
                DepotId = inventory.DepotId!.Value,
                ItemModelId = inventory.ItemModelId!.Value,
                Quantity = inventory.Quantity ?? 0
            })
            .ToDictionaryAsync(
                inventory => (inventory.DepotId, inventory.ItemModelId),
                inventory => inventory.Quantity,
                cancellationToken);
    }

    private static int? TryGetRemainingQuantity(
        IReadOnlyDictionary<(int DepotId, int ItemModelId), int> currentQuantityMap,
        InventoryLog log)
    {
        var depotId = ResolveDepotId(log);
        var itemModelId = log.ItemModelId
                          ?? log.SupplyInventory?.ItemModelId
                          ?? log.ReusableItem?.ItemModelId;

        if (!depotId.HasValue || !itemModelId.HasValue)
        {
            return null;
        }

        return currentQuantityMap.TryGetValue((depotId.Value, itemModelId.Value), out var quantity)
            ? quantity
            : null;
    }

    private static int? ResolveDepotId(InventoryLog log)
    {
        return log.SupplyInventory?.DepotId ?? log.ReusableItem?.DepotId;
    }

    private static ItemModel? ResolveItemModel(InventoryLog log)
    {
        return log.ItemModel
               ?? log.SupplyInventory?.ItemModel
               ?? log.ReusableItem?.ItemModel;
    }

    private static bool IsUnitLevelReusableAction(InventoryLog log)
    {
        return log.ReusableItemId.HasValue
               && (log.QuantityChange ?? 0) == 1;
    }

    private static string BuildTransactionGroupKey(InventoryLog log)
    {
        var actionType = (log.ActionType ?? string.Empty).ToLowerInvariant();
        var sourceType = (log.SourceType ?? string.Empty).ToLowerInvariant();
        var performedBy = log.PerformedBy?.ToString() ?? "system";

        if (actionType == nameof(InventoryActionType.Import).ToLowerInvariant()
            && log.VatInvoiceId.HasValue)
        {
            return $"import|purchase|vat:{log.VatInvoiceId.Value}|by:{performedBy}";
        }

        if (actionType == nameof(InventoryActionType.Import).ToLowerInvariant()
            && sourceType == nameof(InventorySourceType.Donation).ToLowerInvariant()
            && log.SourceId.HasValue)
        {
            var batchTime = log.CreatedAt?.ToUniversalTime().ToString("yyyyMMddHHmmss") ?? "no-created-at";
            return $"import|donation|source:{log.SourceId.Value}|by:{performedBy}|batch:{batchTime}";
        }

        var createdAtBucket = log.CreatedAt?.ToUniversalTime().ToString("yyyyMMddHHmmss") ?? "no-created-at";
        return $"{actionType}|{sourceType}|source:{log.SourceId?.ToString() ?? "none"}|by:{performedBy}|created:{createdAtBucket}";
    }

    /// <inheritdoc/>
    public async Task<List<InventoryMovementDataPoint>> GetDailyMovementChartAsync(
        int depotId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyDepotFilter(_unitOfWork.Set<InventoryLog>(), depotId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toUtc.Value);
        }

        var rawRows = (await query
            .Select(x => new
            {
                CreatedAt = x.CreatedAt!.Value,
                ActionType = x.ActionType ?? string.Empty,
                QuantityChange = x.QuantityChange ?? 0
            })
            .ToListAsync(cancellationToken))
            .Select(x => new
            {
                Date = DateOnly.FromDateTime(x.CreatedAt),
                x.ActionType,
                x.QuantityChange
            })
            .ToList();

        if (rawRows.Count == 0)
        {
            return [];
        }

        var effectiveFrom = fromUtc.HasValue
            ? DateOnly.FromDateTime(fromUtc.Value)
            : rawRows.Min(x => x.Date);
        var effectiveTo = toUtc.HasValue
            ? DateOnly.FromDateTime(toUtc.Value)
            : rawRows.Max(x => x.Date);

        var grouped = rawRows
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<InventoryMovementDataPoint>();
        for (var day = effectiveFrom; day <= effectiveTo; day = day.AddDays(1))
        {
            var point = new InventoryMovementDataPoint { Date = day };

            if (grouped.TryGetValue(day, out var rows))
            {
                foreach (var row in rows)
                {
                    var action = row.ActionType.ToLowerInvariant();
                    var quantity = Math.Abs(row.QuantityChange);

                    if (InventoryLogMetadataMappings.CountsAsInboundMovement(action))
                    {
                        point.TotalIn += quantity;
                    }
                    else if (InventoryLogMetadataMappings.CountsAsOutboundMovement(action))
                    {
                        point.TotalOut += quantity;
                    }
                    else if (action == nameof(InventoryActionType.Adjust).ToLowerInvariant())
                    {
                        point.TotalAdjust += row.QuantityChange;
                    }
                }
            }

            result.Add(point);
        }

        return result;
    }

    private IQueryable<InventoryLog> ApplyDepotFilter(IQueryable<InventoryLog> query, int depotId)
    {
        var supplyRequests = _unitOfWork.Set<DepotSupplyRequest>();
        var depotClosures = _unitOfWork.Set<DepotClosure>();
        var transferSourceType = InventorySourceType.Transfer.ToString();
        var depotClosureSourceType = InventorySourceType.DepotClosure.ToString();
        var reserveActionType = InventoryActionType.Reserve.ToString();
        var transferOutActionType = InventoryActionType.TransferOut.ToString();
        var transferInActionType = InventoryActionType.TransferIn.ToString();

        return query.Where(log =>
            (
                log.DepotSupplyInventoryId != null
                && (
                    (
                            log.SourceType == transferSourceType
                            && (
                                (
                                    (log.ActionType == reserveActionType || log.ActionType == transferOutActionType)
                                    && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.SourceDepotId == depotId)
                                )
                                || (
                                    log.ActionType == transferInActionType
                                    && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.RequestingDepotId == depotId)
                                )
                            )
                    )
                    || (
                            log.SourceType == depotClosureSourceType
                            && (
                                (
                                    log.ActionType == transferOutActionType
                                    && depotClosures.Any(dc => dc.Id == log.SourceId && dc.DepotId == depotId)
                                )
                                || (log.ActionType == transferInActionType && log.SupplyInventory!.DepotId == depotId)
                            )
                        )
                    || (
                        log.SourceType != transferSourceType
                        && log.SourceType != depotClosureSourceType
                        && log.SupplyInventory!.DepotId == depotId
                    )
                )
            )
            || (
                log.ReusableItemId != null
                && (
                    (
                            log.SourceType == transferSourceType
                            && (
                                (
                                    (log.ActionType == reserveActionType || log.ActionType == transferOutActionType)
                                    && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.SourceDepotId == depotId)
                                )
                                || (
                                    log.ActionType == transferInActionType
                                    && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.RequestingDepotId == depotId)
                                )
                            )
                    )
                    || (
                            log.SourceType == depotClosureSourceType
                            && (
                                (
                                    log.ActionType == transferOutActionType
                                    && depotClosures.Any(dc => dc.Id == log.SourceId && dc.DepotId == depotId)
                                )
                                || (log.ActionType == transferInActionType && log.ReusableItem!.DepotId == depotId)
                            )
                        )
                    || (
                        log.SourceType != transferSourceType
                        && log.SourceType != depotClosureSourceType
                        && log.ReusableItem!.DepotId == depotId
                    )
                )
            ));
    }

    private static string FormatReadableTransactionId(InventoryLog log, DateTime? createdAt)
    {
        var timeValue = createdAt ?? DateTime.UtcNow;
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(timeValue.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(timeValue, DateTimeKind.Utc) : timeValue.ToUniversalTime(), vnTimeZone);

        var dateStr = localTime.ToString("yyMMdd-HHmm");
        var actStr = (log.ActionType ?? "UNK").ToUpperInvariant();
        var srcStr = (log.SourceType ?? "UNK").ToUpperInvariant();

        string prefix;
        int id = 0;

        if (actStr == "IMPORT" && log.VatInvoiceId.HasValue)
        {
            prefix = "VAT";
            id = log.VatInvoiceId.Value;
        }
        else if (srcStr == "DONATION" && log.SourceId.HasValue)
        {
            prefix = "DON";
            id = log.SourceId.Value;
        }
        else if (srcStr == "MISSION" && log.SourceId.HasValue)
        {
            prefix = "MIS";
            id = log.SourceId.Value;
        }
        else if (srcStr == "TRANSFER" && log.SourceId.HasValue)
        {
            prefix = "TRA";
            id = log.SourceId.Value;
        }
        else if (actStr == "ADJUST")
        {
            prefix = "ADJ";
            id = log.SourceId ?? 0;
        }
        else
        {
            prefix = actStr.Length >= 3 ? actStr[..3] : actStr;
            id = log.SourceId ?? 0;
        }

        return id > 0 ? $"TX{dateStr}-{prefix}{id}" : $"TX{dateStr}-{prefix}";
    }
}
