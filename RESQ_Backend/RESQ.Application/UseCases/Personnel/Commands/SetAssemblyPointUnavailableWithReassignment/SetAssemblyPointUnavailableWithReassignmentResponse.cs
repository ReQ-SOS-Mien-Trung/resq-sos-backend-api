namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailableWithReassignment;

public sealed class SetAssemblyPointUnavailableWithReassignmentResponse
{
    public int AssemblyPointId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ReassignedRescuerCount { get; set; }
    public int ReassignedStationedTeamCount { get; set; }
    public int ReassignedMissionActivityCount { get; set; }
    public int NotifiedUserCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
