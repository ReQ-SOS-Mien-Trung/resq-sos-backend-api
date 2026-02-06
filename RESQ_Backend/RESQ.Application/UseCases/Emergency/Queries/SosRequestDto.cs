namespace RESQ.Application.UseCases.Emergency.Queries;

public class SosRequestDto
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PriorityLevel { get; set; }
    public int? WaitTimeMinutes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}