namespace RESQ.Infrastructure.Services;

internal sealed class GoongRoute
{
    public string? Summary { get; set; }
    public List<GoongLeg>? Legs { get; set; }
    public GoongPolyline? OverviewPolyline { get; set; }
}
