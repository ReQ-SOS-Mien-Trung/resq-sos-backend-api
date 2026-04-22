using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
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
            query = query.Where(x =>
                (x.SupplyInventory != null && x.SupplyInventory.ItemModelId == itemModelId.Value)
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

        var logs = rawLogs.Select(x => new InventoryLogModel
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
            ItemModelId = x.SupplyInventory?.ItemModelId ?? x.ReusableItem?.ItemModelId,
            ItemModelName = x.SupplyInventory?.ItemModel?.Name ?? x.ReusableItem?.ItemModel?.Name ?? string.Empty,
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
        }).ToList();

        return new PagedResult<InventoryLogModel>(logs, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<InventoryTransactionDto>> GetTransactionHistoryAsync(
        int? depotId,
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

        var groups = rawLogs
            .GroupBy(x => new
            {
                x.CreatedAt,
                x.ActionType,
                x.SourceType,
                x.SourceId,
                x.PerformedBy
            })
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
            var transactionId = $"{g.Key.ActionType}_{g.Key.SourceType}_{g.Key.SourceId}_{createdAt:yyyyMMdd_HHmmss}";

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
                Items = g.Select(item => new InventoryTransactionItemDto
                {
                    ItemId = item.SupplyInventory?.ItemModelId
                             ?? item.ReusableItem?.ItemModelId
                             ?? 0,
                    SupplyInventoryLotId = item.SupplyInventoryLotId,
                    ItemName = item.SupplyInventory?.ItemModel?.Name
                               ?? item.ReusableItem?.ItemModel?.Name
                               ?? string.Empty,
                    QuantityChange = item.QuantityChange ?? 0,
                    FormattedQuantityChange = FormatQuantityChange(item.ActionType ?? string.Empty, item.QuantityChange ?? 0),
                    Unit = item.SupplyInventory?.ItemModel?.Unit
                           ?? item.ReusableItem?.ItemModel?.Unit
                           ?? string.Empty,
                    ItemType = item.SupplyInventory?.ItemModel?.ItemType
                               ?? item.ReusableItem?.ItemModel?.ItemType
                               ?? string.Empty,
                    TargetGroup = TargetGroupTranslations.JoinAsVietnamese(
                        (item.SupplyInventory?.ItemModel?.TargetGroups
                         ?? item.ReusableItem?.ItemModel?.TargetGroups
                         ?? Enumerable.Empty<RESQ.Infrastructure.Entities.Logistics.TargetGroup>())
                        .Select(tg => tg.Name)),
                    CategoryName = item.SupplyInventory?.ItemModel?.Category?.Name
                                   ?? item.ReusableItem?.ItemModel?.Category?.Name,
                    ReceivedDate = item.ReceivedDate,
                    ExpiredDate = item.ExpiredDate
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
            "transfer" => $"Transfer #{sourceId.Value}",
            "adjustment" => "Manual Adjustment",
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
        return actionType.ToLower() switch
        {
            "import" or "transferin" or "return" => $"+ {quantityChange}",
            "export" or "transferout" => $"- {Math.Abs(quantityChange)}",
            "adjust" => quantityChange >= 0 ? $"+ {quantityChange}" : $"- {Math.Abs(quantityChange)}",
            _ => quantityChange.ToString()
        };
    }

    private static string? NormalizeMultilineText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.Replace("\\r\\n", "\n").Replace("\\n", "\n");
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

        static bool IsIn(string action) => action is "import" or "transferin" or "return" or "advancereturn";
        static bool IsOut(string action) => action is "export" or "transferout" or "reserve" or "pickup" or "distribute";

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

                    if (IsIn(action))
                    {
                        point.TotalIn += quantity;
                    }
                    else if (IsOut(action))
                    {
                        point.TotalOut += quantity;
                    }
                    else if (action == "adjust")
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
        const string depotClosureSourceType = "DepotClosure";

        return query.Where(log =>
            (
                log.DepotSupplyInventoryId != null
                && (
                    (
                        log.SourceType == transferSourceType
                        && (
                            (
                                (log.ActionType == "Reserve" || log.ActionType == "TransferOut")
                                && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.SourceDepotId == depotId)
                            )
                            || (
                                log.ActionType == "TransferIn"
                                && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.RequestingDepotId == depotId)
                            )
                        )
                    )
                    || (
                        log.SourceType == depotClosureSourceType
                        && (
                            (
                                log.ActionType == "TransferOut"
                                && depotClosures.Any(dc => dc.Id == log.SourceId && dc.DepotId == depotId)
                            )
                            || (log.ActionType == "TransferIn" && log.SupplyInventory!.DepotId == depotId)
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
                                (log.ActionType == "Reserve" || log.ActionType == "TransferOut")
                                && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.SourceDepotId == depotId)
                            )
                            || (
                                log.ActionType == "TransferIn"
                                && supplyRequests.Any(sr => sr.Id == log.SourceId && sr.RequestingDepotId == depotId)
                            )
                        )
                    )
                    || (
                        log.SourceType == depotClosureSourceType
                        && (
                            (
                                log.ActionType == "TransferOut"
                                && depotClosures.Any(dc => dc.Id == log.SourceId && dc.DepotId == depotId)
                            )
                            || (log.ActionType == "TransferIn" && log.ReusableItem!.DepotId == depotId)
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
}
