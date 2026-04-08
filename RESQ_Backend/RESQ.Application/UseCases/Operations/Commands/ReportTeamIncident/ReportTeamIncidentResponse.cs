using System.Text.Json;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

public class ReportTeamIncidentResponse
{
    public int IncidentId { get; set; }
    public int MissionId { get; set; }
    public int MissionTeamId { get; set; }
    public int? MissionActivityId { get; set; }
    public string IncidentScope { get; set; } = "Mission";
    public string IncidentType { get; set; } = string.Empty;
    public string? DecisionCode { get; set; }
    public string Status { get; set; } = "Reported";
    public List<int> IncidentSosRequestIds { get; set; } = [];
    public bool HasSupportRequest { get; set; }
    public int? SupportSosRequestId { get; set; }
    public List<IncidentAffectedActivityDto> AffectedActivities { get; set; } = [];
    public JsonElement? Detail { get; set; }
    public int? AssistanceSosRequestId { get; set; }
    public string? AssistanceSosStatus { get; set; }
    public string? AssistanceSosPriorityLevel { get; set; }
    public DateTime ReportedAt { get; set; }
}
