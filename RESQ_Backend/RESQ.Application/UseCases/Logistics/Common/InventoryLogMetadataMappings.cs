using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Common;

internal static class InventoryLogMetadataMappings
{
    public static string GetActionTypeDisplayName(InventoryActionType actionType) => actionType switch
    {
        InventoryActionType.Import => "Nhập kho",
        InventoryActionType.Export => "Xuất kho",
        InventoryActionType.Adjust => "Điều chỉnh",
        InventoryActionType.TransferIn => "Chuyển đến",
        InventoryActionType.TransferOut => "Chuyển đi",
        InventoryActionType.Return => "Hoàn trả",
        _ => actionType.ToString()
    };

    public static string GetSourceTypeDisplayName(InventorySourceType sourceType) => sourceType switch
    {
        InventorySourceType.Purchase => "Mua hàng",
        InventorySourceType.Donation => "Quyên góp",
        InventorySourceType.Mission => "Nhiệm vụ",
        InventorySourceType.Adjustment => "Điều chỉnh",
        InventorySourceType.Transfer => "Điều chuyển kho",
        InventorySourceType.System => "Hệ thống",
        InventorySourceType.Maintenance => "Bảo trì",
        InventorySourceType.Expired => "Hết hạn",
        InventorySourceType.Damaged => "Hư hỏng",
        InventorySourceType.Disposed => "Thanh lý",
        _ => sourceType.ToString()
    };
}
