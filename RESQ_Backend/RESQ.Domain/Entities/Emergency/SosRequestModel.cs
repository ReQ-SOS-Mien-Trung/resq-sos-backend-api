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
    public string? SenderInfo { get; set; }
    public string? OriginId { get; set; }
    public SosPriorityLevel? PriorityLevel { get; set; }
    public SosRequestStatus Status { get; set; } = SosRequestStatus.Pending;
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
        string? originId = null,
        double? locationAccuracy = null,
        string? sosType = null,
        string? structuredData = null,
        string? networkMetadata = null,
        string? senderInfo = null,
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
            OriginId = originId,
            UserId = userId,
            Location = location,
            LocationAccuracy = locationAccuracy,
            SosType = sosType,
            RawMessage = rawMessage.Trim(),
            StructuredData = structuredData,
            NetworkMetadata = networkMetadata,
            SenderInfo = senderInfo,
            Timestamp = timestamp,
            Status = status,
            PriorityLevel = priorityLevel,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public void SetPriorityLevel(SosPriorityLevel level)
    {
        PriorityLevel = level;
    }

    public void SetStatus(SosRequestStatus status)
    {
        Status = status;
        LastUpdatedAt = DateTime.UtcNow;
    }
}