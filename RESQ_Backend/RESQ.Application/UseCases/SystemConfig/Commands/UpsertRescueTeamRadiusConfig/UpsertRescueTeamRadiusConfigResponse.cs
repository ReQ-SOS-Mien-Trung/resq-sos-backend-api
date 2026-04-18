namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;

public class UpsertRescueTeamRadiusConfigResponse
{
    public double MaxRadiusKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
