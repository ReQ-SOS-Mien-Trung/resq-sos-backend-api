using RESQ.Domain.Enum.Personnel;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

public class CreateTeamRequestDto
{
    public string Name { get; set; } = string.Empty;
    public RescueTeamType Type { get; set; }
    public int AssemblyPointId { get; set; }
    // Xoá ManagedBy, sẽ được tự động lấy từ Access Token
    public int MaxMembers { get; set; }
    public List<AddMemberRequestDto> Members { get; set; } = new();
}
