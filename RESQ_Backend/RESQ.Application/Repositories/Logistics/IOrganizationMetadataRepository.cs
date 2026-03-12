using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IOrganizationMetadataRepository
{
    Task<List<OrganizationModel>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<OrganizationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OrganizationModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<OrganizationModel> CreateAsync(string name, CancellationToken cancellationToken = default);
}
