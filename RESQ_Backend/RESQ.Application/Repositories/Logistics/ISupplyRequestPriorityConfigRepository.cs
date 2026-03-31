namespace RESQ.Application.Repositories.Logistics;

public class SupplyRequestPriorityConfigDto
{
    public int UrgentMinutes { get; set; }
    public int HighMinutes { get; set; }
    public int MediumMinutes { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface ISupplyRequestPriorityConfigRepository
{
    Task<SupplyRequestPriorityConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<SupplyRequestPriorityConfigDto> UpsertAsync(
        int urgentMinutes,
        int highMinutes,
        int mediumMinutes,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}
