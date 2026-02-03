using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.SosRequests;

public class SosRequestModel
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public Guid UserId { get; set; }
    public GeoLocation? Location { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? WaitTimeMinutes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }

    public SosRequestModel() { }

    public static SosRequestModel Create(Guid userId, GeoLocation location, string rawMessage)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("RawMessage is required", nameof(rawMessage));

        return new SosRequestModel
        {
            UserId = userId,
            Location = location,
            RawMessage = rawMessage.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };
    }
}