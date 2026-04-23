namespace RESQ.Presentation.Controllers.Emergency;

using RESQ.Domain.Enum.Emergency;

public class GetSosRequestsQueryParameters
{
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
    public List<SosRequestStatus>? Statuses { get; set; }
    public List<SosPriorityLevel>? Priorities { get; set; }
    public List<SosRequestType>? SosTypes { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
