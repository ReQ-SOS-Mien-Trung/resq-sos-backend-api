using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

public class CreateTeamRequestDto
{
    public string Name { get; set; } = string.Empty;
    public RescueTeamType Type { get; set; }
    public int AssemblyPointId { get; set; }
    public int MaxMembers { get; set; }
    public List<AddMemberRequestDto> Members { get; set; } = new();
}
