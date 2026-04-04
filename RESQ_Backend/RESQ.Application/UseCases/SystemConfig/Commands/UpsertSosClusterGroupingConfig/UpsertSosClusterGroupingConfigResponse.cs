namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;

public class UpsertSosClusterGroupingConfigResponse
{
    public double MaximumDistanceKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}