using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IOrganizationReliefRepository
{
    Task<ReliefItemModel> GetOrCreateReliefItemAsync(ReliefItemModel model, CancellationToken cancellationToken = default);

    Task AddOrganizationReliefItemAsync(OrganizationReliefItemModel model, CancellationToken cancellationToken = default);
    
    Task<List<ReliefItemModel>> GetOrCreateReliefItemsBulkAsync(List<ReliefItemModel> models, CancellationToken cancellationToken = default);
    
    Task AddOrganizationReliefItemsBulkAsync(List<OrganizationReliefItemModel> models, CancellationToken cancellationToken = default);
}
