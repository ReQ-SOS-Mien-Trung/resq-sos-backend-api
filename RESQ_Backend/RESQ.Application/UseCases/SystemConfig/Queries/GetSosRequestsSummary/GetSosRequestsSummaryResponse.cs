namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosRequestsSummary;

public class GetSosRequestsSummaryResponse
{
    public int TotalSosRequests { get; set; }
    public double? ChangePercent { get; set; }
    /// <summary>"increase" | "decrease" | "no_change" | "new"</summary>
    public string ChangeDirection { get; set; } = string.Empty;
    public string ComparisonLabel { get; set; } = string.Empty;
}
