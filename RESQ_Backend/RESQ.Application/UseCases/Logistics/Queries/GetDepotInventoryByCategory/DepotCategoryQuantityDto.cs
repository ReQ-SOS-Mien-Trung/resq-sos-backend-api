namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;

public class DepotCategoryQuantityDto
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
}
