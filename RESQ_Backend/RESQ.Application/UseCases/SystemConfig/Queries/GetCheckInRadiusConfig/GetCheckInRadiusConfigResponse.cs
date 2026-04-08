namespace RESQ.Application.UseCases.SystemConfig.Queries.GetCheckInRadiusConfig;

public class GetCheckInRadiusConfigResponse
{
    public double MaxRadiusMeters { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
