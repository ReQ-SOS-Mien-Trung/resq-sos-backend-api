using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestRepository
{
    Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SosRequestModel>> GetAllPagedAsync(
        int pageNumber,
        int pageSize,
        IReadOnlyCollection<SosRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default);
    Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, SosRequestStatus status, CancellationToken cancellationToken = default);
    Task UpdateStatusByClusterIdAsync(int clusterId, SosRequestStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByCompanionUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
