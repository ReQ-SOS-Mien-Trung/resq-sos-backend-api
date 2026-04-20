using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Domain.Entities.Logistics.Models;
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

        // Filter by depot - cover both Consumable (SupplyInventory) and Reusable (ReusableItem)
        // For Reusable items we CANNOT use ReusableItem.DepotId because it reflects the
        // CURRENT location of the item, not where it was at the time the log was created.
        // e.g. after TransferIn to depot 1, ReusableItem.DepotId = 1 for ALL historical logs
        // (Reserve, TransferOut) that happened at depot 2 - causing them to leak into depot 1's view.
        if (depotId.HasValue)
        {
            var supplyRequests = _unitOfWork.Set<DepotSupplyRequest>();

            query = query.Where(x =>
                // Consumable: SupplyInventory.DepotId is stable (one record per depot+item)
                (x.DepotSupplyInventoryId != null && x.SupplyInventory!.DepotId == depotId.Value)

                // Reusable – SupplyRequest: Reserve + TransferOut belong to the SOURCE depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && (x.ActionType == "Reserve" || x.ActionType == "TransferOut")
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.SourceDepotId == depotId.Value))

                // Reusable – SupplyRequest: TransferIn belongs to the REQUESTING depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && x.ActionType == "TransferIn"
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.RequestingDepotId == depotId.Value))

                // Reusable – non-SupplyRequest (Import, Export, Adjust, etc.):
                // use current DepotId as best-effort (item hasn't moved between depots)
                || (x.ReusableItemId != null && x.SourceType != "SupplyRequest"
                    && x.ReusableItem!.DepotId == depotId.Value)
            );
        }

        // Filter by item model
        if (itemModelId.HasValue)
        {
            query = query.Where(x =>
                (x.SupplyInventory != null && x.SupplyInventory.ItemModelId == itemModelId.Value)
                || (x.ReusableItem != null && x.ReusableItem.ItemModelId == itemModelId.Value));
        }

        // Filter by action types
        if (actionTypes != null && actionTypes.Count > 0)
        {
            var actionTypesLower = actionTypes.Select(a => a.ToLower()).ToList();
            query = query.Where(x => actionTypesLower.Contains(x.ActionType!.ToLower()));
        }

        // Filter by source types
        if (sourceTypes != null && sourceTypes.Count > 0)
        {
            var sourceTypesLower = sourceTypes.Select(s => s.ToLower()).ToList();
            query = query.Where(x => sourceTypesLower.Contains(x.SourceType!.ToLower()));
        }

        // Filter by date range
        if (fromDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt!.Value) <= toDate.Value);
        }

        // Tìm kiếm tự do: ghi chú, tên người thực hiện, thông tin hóa đơn VAT
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Note != null && x.Note.ToLower().Contains(s))
                || (x.PerformedByUser != null &&
                    (x.PerformedByUser.LastName + " " + x.PerformedByUser.FirstName).ToLower().Contains(s))
                || (x.VatInvoice != null && x.VatInvoice.SupplierName != null && x.VatInvoice.SupplierName.ToLower().Contains(s))
                || (x.VatInvoice != null && x.VatInvoice.InvoiceNumber != null && x.VatInvoice.InvoiceNumber.ToLower().Contains(s))
                || (x.VatInvoice != null && x.VatInvoice.InvoiceSerial != null && x.VatInvoice.InvoiceSerial.ToLower().Contains(s))
                || (x.VatInvoice != null && x.VatInvoice.SupplierTaxCode != null && x.VatInvoice.SupplierTaxCode.ToLower().Contains(s))
            );
        }

        // Order by most recent first
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
            ItemModelId   = x.SupplyInventory?.ItemModelId ?? x.ReusableItem?.ItemModelId,
            ItemModelName = x.SupplyInventory?.ItemModel?.Name ?? x.ReusableItem?.ItemModel?.Name ?? string.Empty,
            SerialNumber  = x.ReusableItem?.SerialNumber,
            LotId         = x.SupplyInventoryLot?.Id,
            ReusableItemId = x.ReusableItemId,
            VatInvoiceId  = x.VatInvoiceId,
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

        // Depot filter: consumable logs via SupplyInventory, reusable logs via SupplyRequest depot fields.
        // IMPORTANT: ReusableItem.DepotId reflects the CURRENT location of the item, NOT where
        // it was at the time the log was created. Using it directly would cause historical logs
        // (Reserve, TransferOut at depot 2) to appear in depot 1's view after a TransferIn.
        if (depotId.HasValue)
        {
            var supplyRequests = _unitOfWork.Set<DepotSupplyRequest>();

            query = query.Where(x =>
                // Consumable: SupplyInventory.DepotId is stable
                (x.DepotSupplyInventoryId != null && x.SupplyInventory!.DepotId == depotId.Value)

                // Reusable – SupplyRequest: Reserve + TransferOut belong to the SOURCE depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && (x.ActionType == "Reserve" || x.ActionType == "TransferOut")
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.SourceDepotId == depotId.Value))

                // Reusable – SupplyRequest: TransferIn belongs to the REQUESTING depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && x.ActionType == "TransferIn"
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.RequestingDepotId == depotId.Value))

                // Reusable – non-SupplyRequest (Import, Export, Adjust, etc.):
                // use current DepotId as best-effort (item hasn't moved between depots)
                || (x.ReusableItemId != null && x.SourceType != "SupplyRequest"
                    && x.ReusableItem!.DepotId == depotId.Value)
            );
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

        // Load filtered logs into memory first - EF Core cannot translate GroupBy + g.ToList() to SQL
        var rawLogs = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // Group in memory by transaction boundary
        var groups = rawLogs
            .GroupBy(x => new
            {
                Date = x.CreatedAt?.Date,
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
                Items = g.Select(item =>
                {
                    return new InventoryTransactionItemDto
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
                    };
                }).ToList()
            };
        }).ToList();

        return new PagedResult<InventoryTransactionDto>(result, totalCount, pageNumber, pageSize);
    }

    private string GetSourceName(string? sourceType, int? sourceId)
    {
        if (string.IsNullOrEmpty(sourceType) || !sourceId.HasValue)
            return string.Empty;

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
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Handle historical values saved with literal "\\n" characters
        return text.Replace("\\r\\n", "\n").Replace("\\n", "\n");
    }

    /// <inheritdoc/>
    public async Task<List<InventoryMovementDataPoint>> GetDailyMovementChartAsync(
        int depotId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var supplyRequests = _unitOfWork.Set<DepotSupplyRequest>();

        // Build depot filter query
        var query = _unitOfWork.Set<InventoryLog>()
            .Where(x =>
                // Consumable: SupplyInventory.DepotId is stable
                (x.DepotSupplyInventoryId != null && x.SupplyInventory!.DepotId == depotId)

                // Reusable – SupplyRequest: Reserve + TransferOut belong to SOURCE depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && (x.ActionType == "Reserve" || x.ActionType == "TransferOut")
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.SourceDepotId == depotId))

                // Reusable – SupplyRequest: TransferIn belongs to REQUESTING depot
                || (x.ReusableItemId != null && x.SourceType == "SupplyRequest"
                    && x.ActionType == "TransferIn"
                    && supplyRequests.Any(sr => sr.Id == x.SourceId && sr.RequestingDepotId == depotId))

                // Reusable – non-SupplyRequest: use current DepotId
                || (x.ReusableItemId != null && x.SourceType != "SupplyRequest"
                    && x.ReusableItem!.DepotId == depotId)
            );

        // Apply time range filters only if provided
        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedAt <= toUtc.Value);

        // Pull CreatedAt as DateTime, convert to DateOnly in memory to avoid EF/Npgsql
        // translation issues with DateOnly.FromDateTime() inside SELECT.
        var rawRows = (await query
            .Select(x => new
            {
                CreatedAt      = x.CreatedAt!.Value,
                ActionType     = x.ActionType ?? string.Empty,
                QuantityChange = x.QuantityChange ?? 0
            })
            .ToListAsync(cancellationToken))
            .Select(x => new
            {
                Date           = DateOnly.FromDateTime(x.CreatedAt),
                x.ActionType,
                x.QuantityChange
            })
            .ToList();

        if (rawRows.Count == 0)
            return [];

        // Determine the date range from actual data when caller didn’t provide bounds
        var effectiveFrom = fromUtc.HasValue
            ? DateOnly.FromDateTime(fromUtc.Value)
            : rawRows.Min(x => x.Date);
        var effectiveTo = toUtc.HasValue
            ? DateOnly.FromDateTime(toUtc.Value)
            : rawRows.Max(x => x.Date);

        // Group by date and categorise
        var grouped = rawRows
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Action types that indicate goods leaving the depot (OUT)
        static bool IsIn(string action) => action is "import" or "transferin" or "return" or "advancereturn";
        static bool IsOut(string action) => action is "export" or "transferout" or "reserve" or "pickup" or "distribute";

        // Fill every day in the range, even empty ones
        var result = new List<InventoryMovementDataPoint>();
        for (var d = effectiveFrom; d <= effectiveTo; d = d.AddDays(1))
        {
            var point = new InventoryMovementDataPoint { Date = d };

            if (grouped.TryGetValue(d, out var rows))
            {
                foreach (var r in rows)
                {
                    var action = r.ActionType.ToLowerInvariant();
                    var qty    = Math.Abs(r.QuantityChange);

                    if (IsIn(action))
                        point.TotalIn += qty;
                    else if (IsOut(action))
                        point.TotalOut += qty;
                    else if (action == "adjust")
                        point.TotalAdjust += r.QuantityChange; // preserve sign
                }
            }

            result.Add(point);
        }

        return result;
    }

    
}
