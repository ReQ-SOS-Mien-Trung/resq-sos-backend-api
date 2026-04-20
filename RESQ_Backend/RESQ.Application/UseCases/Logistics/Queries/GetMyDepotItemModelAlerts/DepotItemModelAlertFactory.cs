using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;

public static class DepotItemModelAlertFactory
{
    public static readonly IReadOnlyDictionary<string, int> ExpiringThresholds =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ItemCategoryCode.Medical)] = 90,
            [nameof(ItemCategoryCode.Hygiene)] = 60,
            [nameof(ItemCategoryCode.Food)] = 45,
            [nameof(ItemCategoryCode.Water)] = 30,
            [nameof(ItemCategoryCode.Heating)] = 30
        };

    public static readonly IReadOnlyDictionary<string, int> MaintenanceThresholds =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ItemCategoryCode.Vehicle)] = 180,
            [nameof(ItemCategoryCode.RescueEquipment)] = 180,
            [nameof(ItemCategoryCode.RepairTools)] = 365,
            [nameof(ItemCategoryCode.Shelter)] = 365
        };

    public static List<DepotItemModelAlertDto> BuildAll(
        IEnumerable<ExpiringItemModelAlertRawDto> expiringCandidates,
        IEnumerable<MaintenanceItemModelAlertRawDto> maintenanceCandidates,
        DateTime todayUtcDate)
    {
        var alerts = BuildExpiringAlerts(expiringCandidates, todayUtcDate)
            .Concat(BuildMaintenanceAlerts(maintenanceCandidates, todayUtcDate))
            .OrderBy(x => x.DueInDays)
            .ThenBy(x => x.AlertType)
            .ThenBy(x => x.ItemModelName)
            .ToList();

        return alerts;
    }

    public static IEnumerable<DepotItemModelAlertDto> BuildExpiringAlerts(
        IEnumerable<ExpiringItemModelAlertRawDto> candidates,
        DateTime today)
    {
        return candidates
            .Where(x => ExpiringThresholds.ContainsKey(x.CategoryCode))
            .GroupBy(x => new
            {
                x.ItemModelId,
                x.ItemModelName,
                x.Unit,
                x.CategoryId,
                x.CategoryCode,
                x.CategoryName
            })
            .Select(group =>
            {
                var thresholdDays = ExpiringThresholds[group.Key.CategoryCode];
                var triggeredLots = group
                    .Where(x => x.ExpiredDate.Date <= today.AddDays(thresholdDays))
                    .OrderBy(x => x.ExpiredDate)
                    .ToList();

                if (triggeredLots.Count == 0)
                    return null;

                var nearestExpiry = triggeredLots.Min(x => x.ExpiredDate).Date;
                var affectedQuantity = triggeredLots.Sum(x => x.RemainingQuantity);
                var dueInDays = (nearestExpiry - today).Days;

                return new DepotItemModelAlertDto
                {
                    AlertType = "ExpiringSoon",
                    AlertTypeLabel = "Sắp hết hạn",
                    ItemModelId = group.Key.ItemModelId,
                    ItemModelName = group.Key.ItemModelName,
                    Unit = group.Key.Unit,
                    CategoryId = group.Key.CategoryId,
                    CategoryCode = group.Key.CategoryCode,
                    CategoryName = group.Key.CategoryName,
                    ThresholdDays = thresholdDays,
                    ReferenceDate = nearestExpiry,
                    DueDate = nearestExpiry,
                    DueInDays = dueInDays,
                    AffectedQuantity = affectedQuantity,
                    AffectedRecordCount = triggeredLots.Count,
                    ActionableQuantity = affectedQuantity,
                    Message = dueInDays < 0
                        ? $"{group.Key.ItemModelName} có {affectedQuantity} {group.Key.Unit} đã quá hạn ở {triggeredLots.Count} lô."
                        : $"{group.Key.ItemModelName} có {affectedQuantity} {group.Key.Unit} sẽ chạm hạn dùng trong {Math.Max(dueInDays, 0)} ngày tới."
                };
            })
            .Where(x => x is not null)!
            .Cast<DepotItemModelAlertDto>();
    }

    public static IEnumerable<DepotItemModelAlertDto> BuildMaintenanceAlerts(
        IEnumerable<MaintenanceItemModelAlertRawDto> candidates,
        DateTime today)
    {
        return candidates
            .Where(x => MaintenanceThresholds.ContainsKey(x.CategoryCode))
            .GroupBy(x => new
            {
                x.ItemModelId,
                x.ItemModelName,
                x.Unit,
                x.CategoryId,
                x.CategoryCode,
                x.CategoryName
            })
            .Select(group =>
            {
                var thresholdDays = MaintenanceThresholds[group.Key.CategoryCode];
                var dueItems = group
                    .Select(item =>
                    {
                        var referenceDate = (item.LastMaintenanceAt ?? item.CreatedAt)?.Date;
                        if (!referenceDate.HasValue)
                            return null;

                        var dueDate = referenceDate.Value.AddDays(thresholdDays);
                        return new
                        {
                            Item = item,
                            ReferenceDate = referenceDate.Value,
                            DueDate = dueDate,
                            DueInDays = (dueDate - today).Days
                        };
                    })
                    .Where(x => x is not null && x.DueDate <= today)
                    .OrderBy(x => x!.DueDate)
                    .ToList();

                if (dueItems.Count == 0)
                    return null;

                var actionableQuantity = dueItems.Count(x => string.Equals(x!.Item.Status, ReusableItemStatus.Available.ToString(), StringComparison.OrdinalIgnoreCase));
                var firstDue = dueItems[0]!;

                return new DepotItemModelAlertDto
                {
                    AlertType = "MaintenanceDue",
                    AlertTypeLabel = "Đến kỳ bảo trì",
                    ItemModelId = group.Key.ItemModelId,
                    ItemModelName = group.Key.ItemModelName,
                    Unit = group.Key.Unit,
                    CategoryId = group.Key.CategoryId,
                    CategoryCode = group.Key.CategoryCode,
                    CategoryName = group.Key.CategoryName,
                    ThresholdDays = thresholdDays,
                    ReferenceDate = firstDue.ReferenceDate,
                    DueDate = firstDue.DueDate,
                    DueInDays = firstDue.DueInDays,
                    AffectedQuantity = dueItems.Count,
                    AffectedRecordCount = dueItems.Count,
                    ActionableQuantity = actionableQuantity,
                    Message = actionableQuantity > 0
                        ? $"{group.Key.ItemModelName} có {actionableQuantity}/{dueItems.Count} thiết bị đã đến kỳ bảo trì và có thể xử lý ngay."
                        : $"{group.Key.ItemModelName} đã đến kỳ bảo trì nhưng hiện chưa có thiết bị nào ở trạng thái sẵn sàng để chuyển bảo trì."
                };
            })
            .Where(x => x is not null)!
            .Cast<DepotItemModelAlertDto>();
    }
}
