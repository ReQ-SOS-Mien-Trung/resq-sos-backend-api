namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuersDailyStatistics;

public class GetRescuersDailyStatisticsResponse
{
    public int TotalRescuers { get; set; }
    public DailyChangeDto DailyChange { get; set; } = null!;

    public class DailyChangeDto
    {
        public int CurrentCount { get; set; }
        public int PreviousCount { get; set; }
        public int ChangeValue { get; set; }
        public double? ChangePercent { get; set; }
        /// <summary>"increase" | "decrease" | "no_change" | "new"</summary>
        public string ChangeDirection { get; set; } = string.Empty;
        public string ComparisonPeriod { get; set; } = "yesterday";
        public string ComparisonLabel { get; set; } = string.Empty;
    }
}
