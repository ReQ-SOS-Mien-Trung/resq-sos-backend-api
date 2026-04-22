namespace RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;

/// <summary>
/// Thông tin sự kiện tập trung sắp tới (Gathering) dành cho rescuer.
/// </summary>
public class UpcomingAssemblyEventDto
{
    public int EventId { get; set; }
    public int AssemblyPointId { get; set; }
    public string AssemblyPointName { get; set; } = string.Empty;
    public string? AssemblyPointCode { get; set; }
    public string? AssemblyPointImageUrl { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }

    /// <summary>Thời điểm triệu tập (giờ Việt Nam).</summary>
    public DateTime AssemblyDate { get; set; }

    /// <summary>Hạn chót check-in (giờ Việt Nam).</summary>
    public DateTime? CheckInDeadline { get; set; }

    /// <summary>Trạng thái sự kiện.</summary>
    public string EventStatus { get; set; } = string.Empty;

    /// <summary>Rescuer đã check-in chưa.</summary>
    public bool IsCheckedIn { get; set; }

    /// <summary>Thời điểm check-in (nếu đã check-in).</summary>
    public DateTime? CheckInTime { get; set; }
}
