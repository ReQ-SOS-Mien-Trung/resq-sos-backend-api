using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public int DepotId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SupplyExecutionItemDto> ConsumedItems { get; set; } = [];
}
