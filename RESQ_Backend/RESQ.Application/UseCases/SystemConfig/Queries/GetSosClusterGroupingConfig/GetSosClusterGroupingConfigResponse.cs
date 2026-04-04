namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosClusterGroupingConfig;

public class GetSosClusterGroupingConfigResponse
{
    public double MaximumDistanceKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}