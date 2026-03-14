using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class InventoryMovementExportRepository(ResQDbContext context) : IInventoryMovementExportRepository
{
    private readonly ResQDbContext _context = context;

    public async Task<List<InventoryMovementRow>> GetMovementRowsAsync(
        InventoryMovementExportPeriod period,
        int? depotId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryLogs
            .Include(l => l.DepotSupplyInventory)
                .ThenInclude(d => d!.ReliefItem)
                    .ThenInclude(r => r!.ItemCategory)
            .Include(l => l.VatInvoice)
                .ThenInclude(v => v!.VatInvoiceItems)
            .Where(l => l.CreatedAt >= period.From && l.CreatedAt <= period.To)
            .AsQueryable();

        if (depotId.HasValue)
        {
            query = query.Where(l => l.DepotSupplyInventory != null
                                     && l.DepotSupplyInventory.DepotId == depotId.Value);
        }

        var logs = await query
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(log =>
        {
            var ri = log.DepotSupplyInventory?.ReliefItem;
            var quantityChange = log.QuantityChange ?? 0;
            var actionType = log.ActionType ?? string.Empty;

            // UnitPrice: lấy từ VatInvoiceItem khớp với ReliefItemId (nếu có)
            decimal? unitPrice = null;
            if (log.VatInvoice != null && ri != null)
            {
                unitPrice = log.VatInvoice.VatInvoiceItems
                    .FirstOrDefault(vi => vi.ReliefItemId == ri.Id)
                    ?.UnitPrice;
            }

            return new InventoryMovementRow
            {
                ItemName      = ri?.Name ?? string.Empty,
                Category      = ri?.ItemCategory?.Name ?? string.Empty,
                TargetGroup   = ri?.TargetGroup ?? string.Empty,
                ItemType      = ri?.ItemType ?? string.Empty,
                Unit          = ri?.Unit ?? string.Empty,
                UnitPrice     = unitPrice,
                QuantityChange    = quantityChange,
                FormattedQuantity = FormatQuantity(actionType, quantityChange),
                CreatedAt     = log.CreatedAt,
                ActionType    = actionType,
                SourceType    = log.SourceType ?? string.Empty,
                MissionName   = log.MissionId.HasValue ? $"Nhiệm vụ #{log.MissionId.Value}" : null,
            };
        }).ToList();
    }

    private static string FormatQuantity(string actionType, int quantityChange)
        => actionType.ToLowerInvariant() switch
        {
            "import" or "transferin" or "return" => $"+{quantityChange}",
            "export" or "transferout"             => $"-{Math.Abs(quantityChange)}",
            "adjust" => quantityChange >= 0 ? $"+{quantityChange}" : $"-{Math.Abs(quantityChange)}",
            _        => quantityChange.ToString()
        };
}
