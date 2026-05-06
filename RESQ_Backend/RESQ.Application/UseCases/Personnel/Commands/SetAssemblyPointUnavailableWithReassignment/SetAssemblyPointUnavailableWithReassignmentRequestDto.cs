namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailableWithReassignment;

public sealed class SetAssemblyPointUnavailableWithReassignmentRequestDto
{
    public string? Reason { get; set; }
    public List<RescuerAssemblyPointReassignmentDto> RescuerReassignments { get; set; } = [];
    public List<TeamAssemblyPointReassignmentDto> TeamReassignments { get; set; } = [];
    public List<MissionActivityAssemblyPointReassignmentDto> MissionActivityReassignments { get; set; } = [];
}

public sealed class RescuerAssemblyPointReassignmentDto
{
    public Guid UserId { get; set; }
    public int TargetAssemblyPointId { get; set; }
}

public sealed class TeamAssemblyPointReassignmentDto
{
    public int RescueTeamId { get; set; }
    public int TargetAssemblyPointId { get; set; }
}

public sealed class MissionActivityAssemblyPointReassignmentDto
{
    public int MissionActivityId { get; set; }
    public int TargetAssemblyPointId { get; set; }
}
