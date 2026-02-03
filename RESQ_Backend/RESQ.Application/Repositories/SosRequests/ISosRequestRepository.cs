using RESQ.Domain.Entities.SosRequests;

namespace RESQ.Application.Repositories.SosRequests;

public interface ISosRequestRepository
{
    Task CreateAsync(SosRequestModel sosRequest, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosRequestModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SosRequestModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}