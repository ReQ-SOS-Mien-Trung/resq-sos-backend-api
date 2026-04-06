namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerRequestDto
{
    /// <summary>UserId của người dùng có role Manager (RoleId = 4) sẽ được gán làm thủ kho.</summary>
    public Guid ManagerId { get; set; }
}
