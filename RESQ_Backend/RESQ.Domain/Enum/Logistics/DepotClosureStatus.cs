namespace RESQ.Domain.Enum.Logistics;

/// <summary>
/// Status of a depot closure record.
/// </summary>
public enum DepotClosureStatus
{
    InProgress,
    Processing,
    TransferPending,
    Completed,
    Cancelled,
    TimedOut,
    Failed
}
