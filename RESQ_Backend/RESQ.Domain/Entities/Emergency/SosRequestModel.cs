using RESQ.Domain.Entities.Emergency.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Domain.Entities.Emergency;

public class SosRequestModel
{
    public int Id { get; set; }
    public Guid? PacketId { get; set; }
    public int? ClusterId { get; set; }
    public Guid UserId { get; set; }
    public GeoLocation? Location { get; set; }
    public double? LocationAccuracy { get; set; }
    public string? SosType { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public string? NetworkMetadata { get; set; }
    public string? PriorityLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? WaitTimeMinutes { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }

    public SosRequestModel() { }

    public static SosRequestModel Create(
        Guid userId,
        GeoLocation location,
        string rawMessage,
        Guid? packetId = null,
        double? locationAccuracy = null,
        string? sosType = null,
        string? structuredData = null,
        string? networkMetadata = null,
        long? timestamp = null,
        SosRequestStatus status = SosRequestStatus.Pending,
        SosPriorityLevel? priorityLevel = null)
    {
        if (userId == Guid.Empty)
            throw new InvalidSosRequestUserException();

        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new InvalidSosRequestMessageException();

        return new SosRequestModel
        {
            PacketId = packetId,
            UserId = userId,
            Location = location,
            LocationAccuracy = locationAccuracy,
            SosType = sosType,
            RawMessage = rawMessage.Trim(),
            StructuredData = structuredData,
            NetworkMetadata = networkMetadata,
            Timestamp = timestamp,
            Status = status.ToString(),
            PriorityLevel = priorityLevel?.ToString(),
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public void SetPriorityLevel(SosPriorityLevel level)
    {
        PriorityLevel = level.ToString();
    }

    public void SetStatus(SosRequestStatus status)
    {
        Status = status.ToString();
        LastUpdatedAt = DateTime.UtcNow;
    }
}