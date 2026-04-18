namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;

public class AssemblyEventListItemDto
{
    public int EventId { get; set; }
    public int AssemblyPointId { get; set; }
    /// <summary>Thời gian có mặt bắt buộc (giờ Việt Nam).</summary>
    public DateTime AssemblyDate { get; set; }
    /// <summary>Hạn chốt check-in (có thể buffer sau AssemblyDate). Giờ Việt Nam.</summary>
    public DateTime? CheckInDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ParticipantCount { get; set; }
    public int CheckedInCount { get; set; }
}
