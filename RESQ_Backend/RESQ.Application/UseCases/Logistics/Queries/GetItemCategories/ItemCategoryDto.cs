namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

public class ItemCategoryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Description { get; set; } = string.Empty;
}
