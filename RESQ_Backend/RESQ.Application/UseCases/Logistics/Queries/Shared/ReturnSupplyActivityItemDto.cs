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
}