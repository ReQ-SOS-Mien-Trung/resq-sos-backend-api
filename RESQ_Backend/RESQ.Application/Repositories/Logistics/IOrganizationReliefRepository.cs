using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IOrganizationReliefRepository
{
    Task<ItemModelRecord> GetOrCreateReliefItemAsync(ItemModelRecord model, CancellationToken cancellationToken = default);

    Task AddOrganizationReliefItemAsync(OrganizationReliefItemModel model, CancellationToken cancellationToken = default);
    
    Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default);

    Task<List<ItemModelRecord>> CreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default);
    
    Task AddOrganizationReliefItemsBulkAsync(List<OrganizationReliefItemModel> models, CancellationToken cancellationToken = default);
}
