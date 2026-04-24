using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Common;

public static class InventoryLogMetadataMappings
{
    public static string GetActionTypeDisplayName(InventoryActionType actionType)
        => GetActionTypeDisplayName(actionType.ToString());

    public static string GetSourceTypeDisplayName(InventorySourceType sourceType)
        => GetSourceTypeDisplayName(sourceType.ToString());

    public static string GetActionTypeDisplayName(string actionType)
        => actionType switch
        {
            nameof(InventoryActionType.Import) => "Nhập kho",
            nameof(InventoryActionType.Export) => "Xuất kho",
            nameof(InventoryActionType.Adjust) => "Điều chỉnh",
            nameof(InventoryActionType.TransferIn) => "Nhận chuyển kho",
            nameof(InventoryActionType.TransferOut) => "Chuyển kho đi",
            nameof(InventoryActionType.Return) => "Hoàn trả",
            nameof(InventoryActionType.Reserve) => "Đặt trữ",
            nameof(InventoryActionType.MissionPickup) => "Xuất cho hoạt động nhiệm vụ",
            nameof(InventoryActionType.DepotClosureExternalDisposal) => "Xuất xử lý bên ngoài khi đóng kho",
            nameof(InventoryActionType.DepotClosureReusableDecommissioned) => "Thanh lý thiết bị khi đóng kho",
            _ => actionType
        };

    public static string GetSourceTypeDisplayName(string sourceType)
        => sourceType switch
        {
            nameof(InventorySourceType.Purchase) => "Mua hàng",
            nameof(InventorySourceType.Donation) => "Quyên góp",
            nameof(InventorySourceType.Mission) => "Nhiệm vụ",
            nameof(InventorySourceType.MissionActivity) => "Hoạt động nhiệm vụ",
            nameof(InventorySourceType.Adjustment) => "Điều chỉnh",
            nameof(InventorySourceType.Transfer) => "Điều chuyển kho",
            nameof(InventorySourceType.DepotClosure) => "Đóng kho",
            nameof(InventorySourceType.System) => "Hệ thống",
            nameof(InventorySourceType.Maintenance) => "Bảo trì",
            nameof(InventorySourceType.Expired) => "Hết hạn",
            nameof(InventorySourceType.Damaged) => "Hư hỏng",
            nameof(InventorySourceType.Disposed) => "Thanh lý",
            _ => sourceType
        };

    public static bool IsPositiveAction(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return false;
        }

        return actionType.Equals(nameof(InventoryActionType.Import), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.TransferIn), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.Return), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNegativeAction(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return false;
        }

        return actionType.Equals(nameof(InventoryActionType.Export), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.TransferOut), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.Reserve), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.MissionPickup), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.DepotClosureExternalDisposal), StringComparison.OrdinalIgnoreCase)
               || actionType.Equals(nameof(InventoryActionType.DepotClosureReusableDecommissioned), StringComparison.OrdinalIgnoreCase);
    }

    public static bool CountsAsInboundMovement(string? actionType)
        => IsPositiveAction(actionType);

    public static bool CountsAsOutboundMovement(string? actionType)
        => IsNegativeAction(actionType);
}
