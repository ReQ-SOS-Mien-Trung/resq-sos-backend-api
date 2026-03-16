namespace RESQ.Infrastructure.Services;

internal sealed class GoongStep
{
    public GoongTextValue? Distance { get; set; }
    public GoongTextValue? Duration { get; set; }
    public GoongLatLng? StartLocation { get; set; }
    public GoongLatLng? EndLocation { get; set; }
    public string? HtmlInstructions { get; set; }
    public string? Maneuver { get; set; }
    public GoongPolyline? Polyline { get; set; }
}
