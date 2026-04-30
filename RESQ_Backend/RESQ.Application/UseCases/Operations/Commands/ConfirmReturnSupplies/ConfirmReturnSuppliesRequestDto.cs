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
    /// <summary>
    /// Actual returned quantity. When lotAllocations is provided, this must match the sum of quantityTaken.
    /// </summary>
    public int Quantity { get; set; }
    /// <summary>Item-level note. Required when returned consumable quantity is less than expected.</summary>
    public string? Note { get; set; }
    /// <summary>Lots returned to the depot. Required when the activity has an expected lot snapshot.</summary>
    public List<ConfirmReturnLotAllocationDto>? LotAllocations { get; set; }
}

public class ConfirmReturnLotAllocationDto
{
    public int LotId { get; set; }
    public int QuantityTaken { get; set; }
}

public class ActualReturnedReusableItemDto
{
    public int ItemModelId { get; set; }
    public List<ActualReturnedReusableUnitDto> Units { get; set; } = [];
}

public class ActualReturnedReusableUnitDto
{
    public int ReusableItemId { get; set; }
    /// <summary>True when the unit was returned; false when the unit was lost during the mission.</summary>
    public bool IsReturned { get; set; } = true;
    /// <summary>Condition of the returned unit. Ignored for lost units.</summary>
    public string? Condition { get; set; }
    /// <summary>Unit-level note. Required when isReturned is false.</summary>
    public string? Note { get; set; }
}
