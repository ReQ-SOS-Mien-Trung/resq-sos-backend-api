namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class IncidentSupportRequestData
{
    public string? RawMessage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<string> SupportTypes { get; set; } = [];
    public string? Situation { get; set; }
    public bool? HasInjured { get; set; }
    public int? AdultCount { get; set; }
    public int? ChildCount { get; set; }
    public int? ElderlyCount { get; set; }
    public List<string>? MedicalIssues { get; set; }
    public string? Address { get; set; }
    public string? AdditionalDescription { get; set; }
}