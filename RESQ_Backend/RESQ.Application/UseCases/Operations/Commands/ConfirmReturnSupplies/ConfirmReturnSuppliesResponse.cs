namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public int DepotId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RestoredSupplyItemDto> RestoredItems { get; set; } = [];
}

public class RestoredSupplyItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
