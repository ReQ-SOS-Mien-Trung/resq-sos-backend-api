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
}

// ── shared primitives ─────────────────────────────────────────────────────────

public class GeoLocationDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class CiviliansWithTeamDto
{
    public bool HasCiviliansWithTeam { get; set; }
    public int? CivilianCount { get; set; }
    public string? CivilianCondition { get; set; }
}

// ── mission incident ──────────────────────────────────────────────────────────

public class MissionIncidentReportRequest
{
    public string? Scope { get; set; }
    public MissionIncidentContextDto? Context { get; set; }
    public string? IncidentType { get; set; }
    public string MissionDecision { get; set; } = string.Empty;
    public MissionTeamStatusDto? TeamStatus { get; set; }
    public UrgentMedicalDto? UrgentMedical { get; set; }
    public VehicleStatusDto? VehicleStatus { get; set; }
    public List<string>? Hazards { get; set; }
    public MissionRescueRequestDto? RescueRequest { get; set; }
    public MissionHandoverDto? Handover { get; set; }
    public string? Note { get; set; }
    public List<string>? Evidence { get; set; }
}

public class MissionIncidentContextDto
{
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public string? MissionTitle { get; set; }
    public string? TeamName { get; set; }
    public string? ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string? ReportedAt { get; set; }
    public GeoLocationDto? Location { get; set; }
    public int? UnfinishedActivityCount { get; set; }
    public CiviliansWithTeamDto? CiviliansWithTeam { get; set; }
}

public class MissionTeamStatusDto
{
    public int TotalMembers { get; set; }
    public int SafeMembers { get; set; }
    public int LightlyInjuredMembers { get; set; }
    public int SeverelyInjuredMembers { get; set; }
    public int ImmobileMembers { get; set; }
    public int MissingContactMembers { get; set; }
}

public class UrgentMedicalDto
{
    public bool NeedsImmediateEmergencyCare { get; set; }
    public List<string>? EmergencyTypes { get; set; }
}

public class VehicleStatusDto
{
    public string? PrimaryVehicleType { get; set; }
    public string? Status { get; set; }
    public string? RetreatCapability { get; set; }
}

public class MissionRescueRequestDto
{
    public List<string>? SupportTypes { get; set; }
    public string? Priority { get; set; }
    public string? EvacuationPriority { get; set; }
}

public class MissionHandoverDto
{
    public bool NeedsMissionTakeover { get; set; }
    public string? UnfinishedWork { get; set; }
    public int? UnfinishedActivityCount { get; set; }
    public List<string>? TransferItems { get; set; }
    public string? NotesForTakeoverTeam { get; set; }
    public string? SafeHandoverPoint { get; set; }
}

// ── activity incident ─────────────────────────────────────────────────────────

public class ActivityIncidentReportRequest
{
    public string? Scope { get; set; }
    public ActivityIncidentContextDto? Context { get; set; }
    public string? IncidentType { get; set; }
    public List<string>? AffectedResources { get; set; }
    public ActivityImpactDto? Impact { get; set; }
    public ActivitySpecificDetailsDto? SpecificDetails { get; set; }
    public ActivitySupportRequestDto? SupportRequest { get; set; }
    public ActivityTeamStatusDto? TeamStatus { get; set; }
    public string? Note { get; set; }
    public List<string>? Evidence { get; set; }
}

public class ActivityIncidentContextDto
{
    public int? MissionId { get; set; }
    public int? MissionTeamId { get; set; }
    public string? MissionTitle { get; set; }
    public string? TeamName { get; set; }
    public string? ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string? ReportedAt { get; set; }
    public GeoLocationDto? Location { get; set; }
    public List<ActivitySnapshotDto>? Activities { get; set; }
}

public class ActivitySnapshotDto
{
    public int ActivityId { get; set; }
    public string? Title { get; set; }
    public string? ActivityType { get; set; }
    public int? Step { get; set; }
}

public class ActivityImpactDto
{
    public bool CanContinueActivity { get; set; }
    public bool NeedSupportSOS { get; set; }
    public bool NeedReassignActivity { get; set; }
}

public class ActivitySpecificDetailsDto
{
    public string? EquipmentDamage { get; set; }
    public string? VehicleDamage { get; set; }
    public string? LostSupply { get; set; }
    public string? StaffingShortage { get; set; }
}

public class ActivitySupportRequestDto
{
    public List<string>? SupportTypes { get; set; }
    public string? Priority { get; set; }
    public ActivitySupportCountsDto? Counts { get; set; }
    public string? MeetupPoint { get; set; }
    public bool? TakeoverNeeded { get; set; }
}

public class ActivitySupportCountsDto
{
    public int? TeamCount { get; set; }
    public int? PeopleCount { get; set; }
    public int? VehicleCount { get; set; }
}

public class ActivityTeamStatusDto
{
    public int? TotalMembers { get; set; }
    public int? AvailableMembers { get; set; }
    public int LightlyInjuredMembers { get; set; }
    public int? UnavailableMembers { get; set; }
    public bool? NeedsMemberEvacuation { get; set; }
}