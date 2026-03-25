namespace RESQ.Application.Common.Models;

public sealed class DepotRealtimeEventEnvelope
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "DepotUpdated";
    public int DepotId { get; set; }
    public int? MissionId { get; set; }
    public long Version { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string PayloadKind { get; set; } = "Full";
    public bool IsCritical { get; set; }
    public bool RequeryRecommended { get; set; } = true;
    public object? Payload { get; set; }
}
