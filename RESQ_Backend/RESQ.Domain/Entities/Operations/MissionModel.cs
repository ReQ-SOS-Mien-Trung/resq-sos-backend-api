namespace RESQ.Domain.Entities.Operations;

public class MissionModel
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public int? PreviousMissionId { get; set; }
    public string? MissionType { get; set; }
    public double? PriorityScore { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? ExpectedEndTime { get; set; }
    public bool? IsCompleted { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<MissionActivityModel> Activities { get; set; } = [];
}
