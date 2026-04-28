namespace RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;

public class RescuerOverviewResponse
{
    public DateTime GeneratedAt { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public RescuerOverviewTotalsDto Totals { get; set; } = new();
    public RescuerOverviewThisMonthDto ThisMonth { get; set; } = new();
    public RescuerOverviewPeakMonthDto PeakMonth { get; set; } = new();
    public List<RescuerOverviewMonthlyDto> Monthly { get; set; } = [];
}

public class RescuerOverviewTotalsDto
{
    public int Total { get; set; }
    public int Core { get; set; }
    public int Volunteer { get; set; }
    public int Active { get; set; }
    public int Banned { get; set; }
}

public class RescuerOverviewThisMonthDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int NewCount { get; set; }
    public int PreviousNewCount { get; set; }
    public int GrowthPercent { get; set; }
}

public class RescuerOverviewPeakMonthDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int NewCount { get; set; }
}

public class RescuerOverviewMonthlyDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int Total { get; set; }
    public int NewCount { get; set; }
    public int Core { get; set; }
    public int Volunteer { get; set; }
}
