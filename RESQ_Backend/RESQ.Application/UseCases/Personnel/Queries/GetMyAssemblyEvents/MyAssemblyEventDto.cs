namespace RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;

public class MyAssemblyEventDto
{
    public int EventId { get; set; }
    public int AssemblyPointId { get; set; }
    public string AssemblyPointName { get; set; } = string.Empty;
    public string? AssemblyPointCode { get; set; }
    public string? AssemblyPointStatus { get; set; }
    public int? AssemblyPointMaxCapacity { get; set; }
    public string? AssemblyPointImageUrl { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }
    /// <summary>Thời gian có mặt bắt buộc (giờ Việt Nam).</summary>
    public DateTime AssemblyDate { get; set; }
    /// <summary>Hạn chốt check-in (có thể buffer sau AssemblyDate). Null nếu không đặt. Giờ Việt Nam.</summary>
    public DateTime? CheckInDeadline { get; set; }
    public string EventStatus { get; set; } = string.Empty;
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime CreatedAt { get; set; }
}
