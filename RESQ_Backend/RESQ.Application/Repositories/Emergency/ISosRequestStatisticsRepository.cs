namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestStatisticsRepository
{
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
