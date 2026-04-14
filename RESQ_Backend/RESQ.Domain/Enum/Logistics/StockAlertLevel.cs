namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// M?c c?nh báo t?n kho v?t ph?m.
/// </summary>
public enum StockAlertLevel
{
    /// <summary>C?nh báo: t? l? kh? d?ng th?p hon ngu?ng warning và chua ch?m ngu?ng danger.</summary>
    Warning,

    /// <summary>Nguy hi?m: t? l? kh? d?ng th?p hon ngu?ng danger c?u hình.</summary>
    Danger
}
