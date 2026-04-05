namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescueTeamRadiusConfig;

public class GetRescueTeamRadiusConfigResponse
{
    public double MaxRadiusKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
