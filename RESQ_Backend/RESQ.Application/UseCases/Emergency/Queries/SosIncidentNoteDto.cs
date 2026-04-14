namespace RESQ.Application.UseCases.Emergency.Queries;

public class SosIncidentNoteDto
{
    public int Id { get; set; }
    public int? TeamIncidentId { get; set; }
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public string? IncidentScope { get; set; }
    public string Note { get; set; } = string.Empty;
    public Guid? ReportedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? TeamName { get; set; }
    public string? ActivityType { get; set; }
}
