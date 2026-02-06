using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRequestRepository
{
    Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SosRequestModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}