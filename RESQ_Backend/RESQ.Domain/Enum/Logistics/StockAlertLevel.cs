namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Mức cảnh báo tồn kho vật phẩm.
/// </summary>
public enum StockAlertLevel
{
    /// <summary>Cảnh báo: tỉ lệ khả dụng thấp hơn ngưỡng warning và chưa chạm ngưỡng danger.</summary>
    Warning,

    /// <summary>Nguy hiểm: tỉ lệ khả dụng thấp hơn ngưỡng danger cấu hình.</summary>
    Danger
}
