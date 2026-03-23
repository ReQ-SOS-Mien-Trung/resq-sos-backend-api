namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Mức cảnh báo tồn kho vật tư.
/// </summary>
public enum StockAlertLevel
{
    /// <summary>Cảnh báo: tỉ lệ khả dụng > 10% và ≤ 25% tổng tồn kho.</summary>
    Warning,

    /// <summary>Nguy hiểm: tỉ lệ khả dụng ≤ 10% tổng tồn kho (bao gồm cả 0).</summary>
    Danger
}
