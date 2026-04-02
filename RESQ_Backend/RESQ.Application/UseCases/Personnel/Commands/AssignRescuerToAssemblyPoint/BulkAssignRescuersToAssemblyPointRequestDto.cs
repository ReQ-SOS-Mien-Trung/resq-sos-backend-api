namespace RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;

public class BulkAssignRescuersToAssemblyPointRequestDto
{
    /// <summary>
    /// Danh sách ID người dùng cần gán. Phải có ít nhất 1 phần tử.
    /// </summary>
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>ID điểm tập kết. Null = gỡ khỏi điểm tập kết hiện tại.</summary>
    public int? AssemblyPointId { get; set; }
}
