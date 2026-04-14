using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Danh sách v?t ph?m v?i thông tin buffer dã du?c c?p nh?t trong snapshot activity.</summary>
    public List<SupplyToCollectDto>? UpdatedSupplies { get; set; }
}
