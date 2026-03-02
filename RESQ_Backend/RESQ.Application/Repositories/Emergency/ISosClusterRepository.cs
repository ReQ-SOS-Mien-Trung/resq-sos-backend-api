using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosClusterRepository
{
    Task<SosClusterModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosClusterModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosClusterModel cluster, CancellationToken cancellationToken = default);
}
