namespace RESQ.Application.Repositories.System;

public class SosClusterGroupingConfigDto
{
    public double MaximumDistanceKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface ISosClusterGroupingConfigRepository
{
    Task<SosClusterGroupingConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<SosClusterGroupingConfigDto> UpsertAsync(
        double maximumDistanceKm,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}