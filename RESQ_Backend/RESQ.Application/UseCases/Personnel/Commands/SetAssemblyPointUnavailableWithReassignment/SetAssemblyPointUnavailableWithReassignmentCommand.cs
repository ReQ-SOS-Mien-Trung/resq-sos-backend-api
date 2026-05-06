using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailableWithReassignment;

public record SetAssemblyPointUnavailableWithReassignmentCommand(
    int AssemblyPointId,
    Guid ChangedBy,
    string? Reason,
    IReadOnlyList<RescuerAssemblyPointReassignmentDto> RescuerReassignments,
    IReadOnlyList<TeamAssemblyPointReassignmentDto> TeamReassignments,
    IReadOnlyList<MissionActivityAssemblyPointReassignmentDto> MissionActivityReassignments)
    : IRequest<SetAssemblyPointUnavailableWithReassignmentResponse>;
