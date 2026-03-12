namespace RESQ.Application.Repositories.Logistics;

public interface IOrganizationReliefRepository
{
    Task<int> GetOrCreateReliefItemAsync(
        int categoryId, 
        string name, 
        string unit, 
        string itemType, 
        string targetGroup, 
        CancellationToken cancellationToken = default);

    Task AddOrganizationReliefItemAsync(
        int organizationId, 
        int reliefItemId, 
        DateOnly? receivedDate, 
        DateOnly? expiredDate, 
        string notes, 
        CancellationToken cancellationToken = default);
}