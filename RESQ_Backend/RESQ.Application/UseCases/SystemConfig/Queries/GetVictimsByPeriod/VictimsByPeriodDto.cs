namespace RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

public class VictimsByPeriodDto
{
    /// <summary>Mốc thời gian đầu kỳ (UTC, đã truncate theo granularity).</summary>
    public DateTime Period { get; set; }

    /// <summary>Tổng số victim = adult + child + elderly trong kỳ.</summary>
    public int TotalVictims { get; set; }
}
