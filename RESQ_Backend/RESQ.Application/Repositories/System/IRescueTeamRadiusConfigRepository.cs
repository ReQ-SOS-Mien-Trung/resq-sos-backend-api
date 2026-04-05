namespace RESQ.Application.Repositories.System;

public class RescueTeamRadiusConfigDto
{
    public double MaxRadiusKm { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IRescueTeamRadiusConfigRepository
{
    Task<RescueTeamRadiusConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<RescueTeamRadiusConfigDto> UpsertAsync(
        double maxRadiusKm,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}
