namespace RESQ.Application.Common.Models;

public class SupplyExecutionLotDto
{
    public int LotId { get; set; }
    public int QuantityTaken { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public int RemainingQuantityAfterExecution { get; set; }
}

public class SupplyExecutionReusableUnitDto
{
    public int ReusableItemId { get; set; }
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Condition { get; set; }
    public string? Note { get; set; }
}

public class SupplyExecutionItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public List<SupplyExecutionLotDto> LotAllocations { get; set; } = [];
    public List<SupplyExecutionReusableUnitDto> ReusableUnits { get; set; } = [];
}

public class MissionSupplyPickupExecutionResult
{
    public List<SupplyExecutionItemDto> Items { get; set; } = [];
}

public class MissionSupplyReservationResult
{
    public List<SupplyExecutionItemDto> Items { get; set; } = [];
}

public class MissionSupplyReturnExecutionItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int ExpectedQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public List<SupplyExecutionReusableUnitDto> ExpectedReusableUnits { get; set; } = [];
    public List<SupplyExecutionReusableUnitDto> ReturnedReusableUnits { get; set; } = [];
}

public class MissionSupplyReturnExecutionResult
{
    public List<MissionSupplyReturnExecutionItemDto> Items { get; set; } = [];
    public bool UsedLegacyFallback { get; set; }
    public bool DiscrepancyRecorded { get; set; }
}