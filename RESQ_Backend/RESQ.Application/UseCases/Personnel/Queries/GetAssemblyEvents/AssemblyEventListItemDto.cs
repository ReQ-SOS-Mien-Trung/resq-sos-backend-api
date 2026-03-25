namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;

public class AssemblyEventListItemDto
{
    public int EventId { get; set; }
    public int AssemblyPointId { get; set; }
    public DateTime AssemblyDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ParticipantCount { get; set; }
    public int CheckedInCount { get; set; }
}
