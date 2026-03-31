namespace RESQ.Application.UseCases.Logistics.Thresholds;

public class WarningBandConfigDto
{
    public int Id { get; set; }
    public List<WarningBandDto> Bands { get; set; } = [];
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
