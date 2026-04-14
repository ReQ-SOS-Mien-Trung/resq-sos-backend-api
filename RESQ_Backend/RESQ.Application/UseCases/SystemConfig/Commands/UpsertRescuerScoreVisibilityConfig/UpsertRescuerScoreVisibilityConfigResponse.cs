namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;

public class UpsertRescuerScoreVisibilityConfigResponse
{
    public int MinimumEvaluationCount { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
