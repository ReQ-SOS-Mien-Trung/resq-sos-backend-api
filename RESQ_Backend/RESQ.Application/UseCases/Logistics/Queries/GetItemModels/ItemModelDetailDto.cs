namespace RESQ.Application.UseCases.Logistics.Queries.GetItemModels;

public class ItemModelDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public decimal VolumePerUnit { get; set; }
    public decimal WeightPerUnit { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> TargetGroups { get; set; } = new();

    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryCode { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
