using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

namespace RESQ.Application.Common.Models;

public sealed class SosRequestRealtimeUpdate
{
    public int RequestId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public int? ClusterId { get; set; }
    public int? PreviousClusterId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public SosRequestDetailDto Snapshot { get; set; } = new();
}
