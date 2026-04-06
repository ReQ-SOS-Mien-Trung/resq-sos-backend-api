using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class InventoryMovementExportRepository(IUnitOfWork unitOfWork) : IInventoryMovementExportRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<InventoryMovementRow>> GetMovementRowsAsync(
        InventoryMovementExportPeriod period,
        int? depotId,
        CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<InventoryLog>()
            .Include(l => l.SupplyInventory)
                .ThenInclude(d => d!.ItemModel)
                    .ThenInclude(r => r!.Category)
            .Include(l => l.SupplyInventory)
                .ThenInclude(d => d!.ItemModel)
                    .ThenInclude(r => r!.TargetGroups)
            .Include(l => l.VatInvoice)
                .ThenInclude(v => v!.VatInvoiceItems)
            .Where(l => l.CreatedAt >= period.From && l.CreatedAt <= period.To);

        if (depotId.HasValue)
        {
            query = query.Where(l => l.SupplyInventory != null
                                     && l.SupplyInventory.DepotId == depotId.Value);
        }

        var logs = await query
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(log =>
        {
            var ri = log.SupplyInventory?.ItemModel;
            var quantityChange = log.QuantityChange ?? 0;
            var actionType = log.ActionType ?? string.Empty;

            // UnitPrice: lấy từ VatInvoiceItem khớp với ItemModelId (nếu có)
            decimal? unitPrice = null;
            if (log.VatInvoice != null && ri != null)
            {
                unitPrice = log.VatInvoice.VatInvoiceItems
                    .FirstOrDefault(vi => vi.ItemModelId == ri.Id)
                    ?.UnitPrice;
            }

            return new InventoryMovementRow
            {
                ItemName      = ri?.Name ?? string.Empty,
                Category      = ri?.Category?.Name ?? string.Empty,
                TargetGroup   = TranslateTargetGroup(ri != null
                    ? string.Join(", ", ri.TargetGroups.Select(tg => tg.Name))
                    : string.Empty),
                ItemType      = TranslateItemType(ri?.ItemType ?? string.Empty),
                Unit          = ri?.Unit ?? string.Empty,
                UnitPrice     = unitPrice,
                QuantityChange    = quantityChange,
                FormattedQuantity = FormatQuantity(actionType, quantityChange),
                CreatedAt     = log.CreatedAt,
                ActionType    = TranslateActionType(actionType),
                SourceType    = TranslateSourceType(log.SourceType ?? string.Empty),
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

    private static string TranslateItemType(string itemType)
        => itemType switch
        {
            "Consumable" => "Tiêu thụ",
            "Reusable"   => "Tái sử dụng",
            _ => itemType
        };

    private static string TranslateActionType(string actionType)
        => actionType switch
        {
            "Import"      => "Nhập kho",
            "Export"      => "Xuất kho",
            "Adjust"      => "Điều chỉnh",
            "TransferIn"  => "Nhận chuyển kho",
            "TransferOut" => "Chuyển kho đi",
            "Return"      => "Hoàn trả",
            "Reserve"     => "Đặt trước",
            _ => actionType
        };

    private static string TranslateTargetGroup(string targetGroup)
    {
        if (string.IsNullOrEmpty(targetGroup)) return targetGroup;
        return TargetGroupTranslations.JoinAsVietnamese(
            targetGroup.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => t.Trim()));
    }

    private static string TranslateSourceType(string sourceType)
        => sourceType switch
        {
            "Purchase"   => "Mua sắm",
            "Donation"   => "Quyên góp",
            "Mission"    => "Nhiệm vụ",
            "Adjustment" => "Điều chỉnh",
            "Transfer"   => "Chuyển kho",
            "System"     => "Hệ thống",
            "SupplyRequest" => "Yêu cầu tiếp tế",
            _ => sourceType
        };
}
