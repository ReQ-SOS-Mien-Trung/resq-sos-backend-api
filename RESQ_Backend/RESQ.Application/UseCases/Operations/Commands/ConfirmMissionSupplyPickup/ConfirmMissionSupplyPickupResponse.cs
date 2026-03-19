namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public int DepotId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ConsumedSupplyItemDto> ConsumedItems { get; set; } = [];
}

public class ConsumedSupplyItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
