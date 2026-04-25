using MediatR;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record CreateRescueTeamCommand(
    string Name,
    RescueTeamType Type,
    int AssemblyPointId,
    Guid ManagedBy,
    int MaxMembers,
    List<AddMemberRequestDto> Members) : IRequest<int>;
