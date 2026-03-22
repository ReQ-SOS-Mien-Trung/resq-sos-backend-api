namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

public class AssignRescuerToAssemblyPointRequestDto
{
    /// <summary>ID điểm tập kết. Null = gỡ khỏi điểm tập kết hiện tại.</summary>
    public int? AssemblyPointId { get; set; }
}
