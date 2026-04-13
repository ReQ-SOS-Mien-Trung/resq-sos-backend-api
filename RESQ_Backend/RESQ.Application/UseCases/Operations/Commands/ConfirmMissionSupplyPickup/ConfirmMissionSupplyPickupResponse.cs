using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Danh sách vật phẩm với thông tin buffer đã được cập nhật trong snapshot activity.</summary>
    public List<SupplyToCollectDto>? UpdatedSupplies { get; set; }
}
