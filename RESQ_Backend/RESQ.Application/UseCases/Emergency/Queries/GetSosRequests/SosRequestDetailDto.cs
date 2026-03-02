using System.Text.Json;
using System.Text.Json.Serialization;

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
    public JsonElement? StructuredData { get; set; }
    public JsonElement? NetworkMetadata { get; set; }
    public JsonElement? SenderInfo { get; set; }
    public string? OriginId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public int? WaitTimeMinutes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
}