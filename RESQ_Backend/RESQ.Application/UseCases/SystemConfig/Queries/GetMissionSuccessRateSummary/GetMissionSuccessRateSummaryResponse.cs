namespace RESQ.Application.UseCases.SystemConfig.Queries.GetMissionSuccessRateSummary;

public class GetMissionSuccessRateSummaryResponse
{
    public double SuccessRate { get; set; }
    public double ChangePercent { get; set; }
    /// <summary>"increase" | "decrease" | "no_change"</summary>
    public string ChangeDirection { get; set; } = string.Empty;
    public string ComparisonLabel { get; set; } = "so với hôm qua";
}
