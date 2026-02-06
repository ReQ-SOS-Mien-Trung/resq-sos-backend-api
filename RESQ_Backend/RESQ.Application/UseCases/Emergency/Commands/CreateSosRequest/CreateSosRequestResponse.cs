namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestResponse
{
    public int Id { get; set; }
    public Guid? PacketId { get; set; }
    public Guid UserId { get; set; }
    public string? SosType { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public string? NetworkMetadata { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    public long? Timestamp { get; set; }
    public DateTime? CreatedAt { get; set; }
}