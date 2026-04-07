using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Emergency;

public class SosRequestVictimUpdateModel
{
    public int Id { get; set; }
    public int SosRequestId { get; set; }
    public Guid? PacketId { get; set; }
    public GeoLocation? Location { get; set; }
    public double? LocationAccuracy { get; set; }
    public string? SosType { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public string? NetworkMetadata { get; set; }
    public string? SenderInfo { get; set; }
    public string? VictimInfo { get; set; }
    public string? ReporterInfo { get; set; }
    public bool IsSentOnBehalf { get; set; }
    public string? OriginId { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? ClientCreatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByMode { get; set; }
}