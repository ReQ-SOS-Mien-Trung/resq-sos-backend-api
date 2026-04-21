namespace RESQ.Application.Common.Models;

public sealed class SupplyRequestRealtimeUpdate
{
    public int RequestId { get; set; }
    public int RequestingDepotId { get; set; }
    public int SourceDepotId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public string? RejectedReason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
