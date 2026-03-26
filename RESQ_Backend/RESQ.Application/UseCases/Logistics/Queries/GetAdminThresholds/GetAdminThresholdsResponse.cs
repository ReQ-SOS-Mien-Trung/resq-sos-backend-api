using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAdminThresholds;

public class GetAdminThresholdsResponse
{
    public ThresholdConfigDto? Global { get; set; }
    public int? DepotId { get; set; }
    public ThresholdConfigDto? Depot { get; set; }
    public List<ThresholdConfigDto> DepotCategories { get; set; } = [];
    public List<ThresholdConfigDto> DepotItems { get; set; } = [];
}
