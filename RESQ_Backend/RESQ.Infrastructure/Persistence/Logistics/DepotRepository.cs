using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Resources;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class DepotRepository(IUnitOfWork unitOfWork) : IDepotRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        var depotEntity = DepotMapper.ToEntity(depotModel);
        await _unitOfWork.GetRepository<Depot>().AddAsync(depotEntity);
    }

    public async Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Depot>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == depotModel.Id, 
            tracked: true, 
            includeProperties: "DepotManagers"
        );

        if (existingEntity != null)
        {
            DepotMapper.UpdateEntity(existingEntity, depotModel);
            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Depot>();
        
        // UPDATED: Pass OrderBy to ensure consistent pagination (Order by LastUpdated DESC)
        var pagedEntities = await repository.GetPagedAsync(
            pageNumber, 
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(d => d.LastUpdatedAt), 
            includeProperties: "DepotManagers"
        );

        var domainItems = pagedEntities.Items
            .Select(DepotMapper.ToDomain)
            .ToList();

        return new PagedResult<DepotModel>(
            domainItems, 
            pagedEntities.TotalCount, 
            pagedEntities.PageNumber, 
            pagedEntities.PageSize
        );
    }

    public async Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Depot>()
            .GetAllByPropertyAsync(null, includeProperties: "DepotManagers");

        return entities.Select(DepotMapper.ToDomain);
    }

    public async Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "DepotManagers");

        if (entity == null) return null;

        return DepotMapper.ToDomain(entity);
    }

    public async Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Depot>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false, includeProperties: "DepotManagers");

        if (entity == null) return null;

        return DepotMapper.ToDomain(entity);
    }
}
