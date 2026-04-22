namespace RESQ.Application.Common.Models;

public sealed class DepotClosureRealtimeUpdate
{
    public int SourceDepotId { get; set; }
    public int? TargetDepotId { get; set; }
    public int? ClosureId { get; set; }
    public int? TransferId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Status { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
