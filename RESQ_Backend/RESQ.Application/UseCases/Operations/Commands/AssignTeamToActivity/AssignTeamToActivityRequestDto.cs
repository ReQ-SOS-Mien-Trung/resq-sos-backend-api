namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;

public class AssignTeamToActivityRequestDto
{
    /// <summary>ID của rescue team (phải đã được assigned vào mission trước).</summary>
    public int RescueTeamId { get; set; }
}
