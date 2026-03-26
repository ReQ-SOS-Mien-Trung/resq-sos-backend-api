namespace RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;

public class MyAssemblyEventDto
{
    public int EventId { get; set; }
    public int AssemblyPointId { get; set; }
    public string AssemblyPointName { get; set; } = string.Empty;
    public DateTime AssemblyDate { get; set; }
    public string EventStatus { get; set; } = string.Empty;
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime CreatedAt { get; set; }
}
