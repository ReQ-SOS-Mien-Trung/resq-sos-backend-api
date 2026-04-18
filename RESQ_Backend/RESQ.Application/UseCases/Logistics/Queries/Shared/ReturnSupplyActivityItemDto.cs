using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.Shared;

public class ReturnSupplyActivityItemDto
{
    public int? ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public int? ActualReturnedQuantity { get; set; }
    public List<SupplyExecutionReusableUnitDto> ExpectedReturnUnits { get; set; } = [];
    public List<SupplyExecutionReusableUnitDto> ReturnedReusableUnits { get; set; } = [];
    /// <summary>
    /// Danh sách lô hàng thực tế đã được xuất cho mission này (từ PickupLotAllocations trong snapshot).
    /// Frontend dùng để pre-fill màn hình xác nhận trả hàng: lô nào, HSD bao nhiêu, xuất bao nhiêu.
    /// </summary>
    public List<SupplyExecutionLotDto> PickupLotAllocations { get; set; } = [];
}
