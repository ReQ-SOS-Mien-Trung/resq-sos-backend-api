namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;

public class UpsertCheckInRadiusConfigResponse
{
    public double MaxRadiusMeters { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
