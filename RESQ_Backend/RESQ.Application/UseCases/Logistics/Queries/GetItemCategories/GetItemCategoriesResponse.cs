namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;

public class GetItemCategoriesResponse
{
    public List<ItemCategoryDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
