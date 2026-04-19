namespace RESQ.Application.Repositories.Emergency;

public interface IClusterAiHistoryRepository
{
    Task DeleteByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);
}
