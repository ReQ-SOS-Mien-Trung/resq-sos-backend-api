namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerRequestDto
{
    /// <summary>
    /// Danh sách UserId của người dùng có role Manager (RoleId = 4) sẽ được gán làm thủ kho.
    /// Có thể truyền 1 hoặc nhiều ID cùng lúc.
    /// </summary>
    public List<Guid> ManagerIds { get; set; } = [];
}
