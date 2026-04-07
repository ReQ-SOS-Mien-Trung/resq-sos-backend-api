using System.Text.Json.Serialization;
using RESQ.Application.UseCases.Emergency.Queries;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class SosRequestDetailDto
{
    public int Id { get; set; }
    public Guid? PacketId { get; set; }
    public int? ClusterId { get; set; }
    public Guid UserId { get; set; }
    public string? SosType { get; set; }
    [JsonPropertyName("msg")]
    public string RawMessage { get; set; } = string.Empty;
    public SosStructuredDataDto? StructuredData { get; set; }
    public SosNetworkMetadataDto? NetworkMetadata { get; set; }
    public SosSenderInfoDto? SenderInfo { get; set; }
    public SosReporterInfoDto? ReporterInfo { get; set; }
    public SosVictimInfoDto? VictimInfo { get; set; }
    public bool IsSentOnBehalf { get; set; }
    public string? OriginId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public Guid? CreatedByCoordinatorId { get; set; }
    public string? LatestIncidentNote { get; set; }
    public DateTime? LatestIncidentAt { get; set; }
    public List<SosIncidentNoteDto>? IncidentHistory { get; set; }
    public List<CompanionResultDto>? Companions { get; set; }
}