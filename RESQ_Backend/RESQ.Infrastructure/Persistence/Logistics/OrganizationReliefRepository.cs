using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class OrganizationReliefRepository(IUnitOfWork unitOfWork) : IOrganizationReliefRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> GetOrCreateReliefItemAsync(
        int categoryId, 
        string name, 
        string unit, 
        string itemType, 
        string targetGroup, 
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<ReliefItem>();
        
        // 1. Check if the relief item already exists
        var item = await repo.GetByPropertyAsync(
            r => r.Name == name && r.CategoryId == categoryId, 
            tracked: true);

        // 2. If not, create it
        if (item == null)
        {
            item = new ReliefItem
            {
                CategoryId = categoryId,
                Name = name,
                Unit = unit,
                ItemType = itemType,
                TargetGroup = targetGroup,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await repo.AddAsync(item);
            await _unitOfWork.SaveAsync();
        }

        return item.Id;
    }

    public async Task AddOrganizationReliefItemAsync(
        int organizationId, 
        int reliefItemId, 
        DateOnly? receivedDate, 
        DateOnly? expiredDate, 
        string notes, 
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<OrganizationReliefItem>();
        
        var entity = new OrganizationReliefItem
        {
            OrganizationId = organizationId,
            ReliefItemId = reliefItemId,
            ReceivedDate = receivedDate,
            ExpiredDate = expiredDate,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveAsync();
    }
}