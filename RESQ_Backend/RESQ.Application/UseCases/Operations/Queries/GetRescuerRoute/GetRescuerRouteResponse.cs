using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

public class GetRescuerRouteResponse
{
    public int ActivityId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public double OriginLatitude { get; set; }
    public double OriginLongitude { get; set; }
    public string Vehicle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public GoongRouteSummary? Route { get; set; }
}
