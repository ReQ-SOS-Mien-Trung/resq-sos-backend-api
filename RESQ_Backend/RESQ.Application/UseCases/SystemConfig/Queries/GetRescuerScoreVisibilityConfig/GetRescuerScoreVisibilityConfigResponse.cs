namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerScoreVisibilityConfig;

public class GetRescuerScoreVisibilityConfigResponse
{
    public int MinimumEvaluationCount { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
