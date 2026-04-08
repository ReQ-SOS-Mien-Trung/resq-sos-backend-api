namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesRequestDto
{
    public string? DiscrepancyNote { get; set; }
    public List<ActualReturnedConsumableItemDto> ConsumableItems { get; set; } = [];
    public List<ActualReturnedReusableItemDto> ReusableItems { get; set; } = [];
}

public class ActualReturnedConsumableItemDto
{
    public int ItemModelId { get; set; }
    public int Quantity { get; set; }
}

public class ActualReturnedReusableItemDto
{
    public int ItemModelId { get; set; }
    public int? Quantity { get; set; }
    public List<ActualReturnedReusableUnitDto> Units { get; set; } = [];
}

public class ActualReturnedReusableUnitDto
{
    public int ReusableItemId { get; set; }
    public string? SerialNumber { get; set; }
}