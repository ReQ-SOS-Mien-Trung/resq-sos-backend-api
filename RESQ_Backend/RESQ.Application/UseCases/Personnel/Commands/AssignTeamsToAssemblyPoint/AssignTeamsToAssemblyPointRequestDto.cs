namespace RESQ.Application.UseCases.Personnel.Commands.AssignTeamsToAssemblyPoint;

public class AssignTeamsToAssemblyPointRequestDto
{
    /// <summary>Danh sách ID của các đội cứu hộ cần gán vào điểm tập kết.</summary>
    public List<int> TeamIds { get; set; } = new();
}
