namespace RESQ.Application.Repositories.System;

public class CheckInRadiusConfigDto
{
    public double MaxRadiusMeters { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface ICheckInRadiusConfigRepository
{
    Task<CheckInRadiusConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<CheckInRadiusConfigDto> UpsertAsync(
        double maxRadiusMeters,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}
