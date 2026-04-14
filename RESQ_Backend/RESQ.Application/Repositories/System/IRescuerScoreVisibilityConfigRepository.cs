namespace RESQ.Application.Repositories.System;

public class RescuerScoreVisibilityConfigDto
{
    public int MinimumEvaluationCount { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IRescuerScoreVisibilityConfigRepository
{
    Task<RescuerScoreVisibilityConfigDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<RescuerScoreVisibilityConfigDto> UpsertAsync(
        int minimumEvaluationCount,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}
