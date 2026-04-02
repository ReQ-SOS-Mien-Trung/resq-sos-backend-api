namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

/// <summary>
/// Request body cho API điều chỉnh tồn kho.
/// QuantityChange dương = tăng (tạo lô mới), âm = giảm (FEFO deduction).
/// ExpiredDate chỉ dùng khi QuantityChange > 0 để đặt hạn dùng cho lô mới.
/// </summary>
public record AdjustInventoryRequest(
    int ItemModelId,
    int QuantityChange,
    string Reason,
    string? Note,
    DateTime? ExpiredDate);
