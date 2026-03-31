namespace RESQ.Application.UseCases.Logistics.Thresholds;

public class WarningBandDto
{
    public string Name { get; set; } = string.Empty;
    public decimal From { get; set; }
    public decimal? To { get; set; }
}
