using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>ID cua RETURN_SUPPLIES activity duoc tao/cap nhat neu con hang can tra, null neu khong co.</summary>
    public int? SurplusReturnActivityId { get; set; }

    /// <summary>Chi tiet so luong ke hoach va thuc te cua tung loai vat pham.</summary>
    public List<DeliveryItemResultDto> DeliveredItems { get; set; } = [];
}

public class DeliveryItemResultDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int PlannedQuantity { get; set; }
    public int ActualDeliveredQuantity { get; set; }
    public int SurplusQuantity { get; set; }
    public List<SupplyExecutionLotDto> DeliveredLotAllocations { get; set; } = [];
    public List<SupplyExecutionReusableUnitDto> DeliveredReusableUnits { get; set; } = [];
}
