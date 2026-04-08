using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;

namespace RESQ.Application.UseCases.Operations.Shared;

public static class IncidentV2Constants
{
    public const int PayloadVersion = 2;
    public const string MissionIncidentType = "mission_incident";
    public const string ActivityIncidentType = "activity_incident";

    public static class MissionDecisionCodes
    {
        public const string ContinueMission = "continue_mission";
        public const string PauseMission = "pause_mission";
        public const string StopMission = "stop_mission";
        public const string HandoverMission = "handover_mission";
        public const string RescueWholeTeamImmediately = "rescue_whole_team_immediately";
    }

    public static class ActivityDecisionCodes
    {
        public const string ContinueActivity = "continue_activity";
        public const string CannotContinueActivity = "cannot_continue_activity";
        public const string ReassignActivity = "reassign_activity";
    }

    public static class SupportTypes
    {
        public const string MedicalSupport = "medical_support";
        public const string SupplySupport = "supply_support";
        public const string VehicleSupport = "vehicle_support";
        public const string FuelSupport = "fuel_support";
        public const string TakeoverActivity = "takeover_activity";
    }

    public static class RetreatCapabilityCodes
    {
        public const string UrgentRescueNeeded = "urgent_rescue_needed";
    }
}

public class MissionIncidentReportRequest
{
    public string? Summary { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string MissionDecision { get; set; } = string.Empty;
    public MissionIncidentTeamConditionDto? TeamCondition { get; set; }
    public MissionIncidentHandoverDto? Handover { get; set; }
    public bool NeedSupportSos { get; set; }
    public IncidentSupportRequestData? SupportRequest { get; set; }
}

public class MissionIncidentTeamConditionDto
{
    public bool HasInjuredMember { get; set; }
    public string? RetreatCapability { get; set; }
    public string? Situation { get; set; }
    public string? AdditionalDescription { get; set; }
}

public class MissionIncidentHandoverDto
{
    public string? Reason { get; set; }
    public string? RequestedTeamType { get; set; }
    public string? Note { get; set; }
}

public class ActivityIncidentReportRequest
{
    public string? Summary { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<int> ActivityIds { get; set; } = [];
    public int? PrimaryActivityId { get; set; }
    public bool CanContinueActivity { get; set; }
    public bool NeedReassignActivity { get; set; }
    public bool NeedSupportSos { get; set; }
    public bool? HasInjuredMember { get; set; }
    public string? ImpactCode { get; set; }
    public string? Situation { get; set; }
    public string? AdditionalDescription { get; set; }
    public IncidentSupportRequestData? SupportRequest { get; set; }
}