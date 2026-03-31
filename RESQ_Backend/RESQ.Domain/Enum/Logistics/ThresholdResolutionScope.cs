namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Scope mà minimumThreshold được resolve từ.
/// </summary>
public enum ThresholdResolutionScope
{
    Item     = 0,
    Category = 1,
    Depot    = 2,
    Global   = 3,
    None     = 4
}
