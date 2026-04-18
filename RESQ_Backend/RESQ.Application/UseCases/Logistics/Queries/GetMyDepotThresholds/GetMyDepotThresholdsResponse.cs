namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;

public class GetMyDepotThresholdsResponse
{
    public int DepotId { get; set; }
    public ThresholdConfigDto? Global { get; set; }
    public ThresholdConfigDto? Depot { get; set; }
    public List<ThresholdConfigDto> DepotCategories { get; set; } = new();
    public List<ThresholdConfigDto> DepotItems { get; set; } = new();
}

public class ThresholdConfigDto
{
    public int Id { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public int? MinimumThreshold { get; set; }
    public uint RowVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}
