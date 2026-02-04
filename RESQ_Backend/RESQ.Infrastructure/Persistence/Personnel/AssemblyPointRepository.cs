using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Mappers.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class AssemblyPointRepository(IUnitOfWork unitOfWork) : IAssemblyPointRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var entity = AssemblyPointMapper.ToEntity(model);
        await _unitOfWork.GetRepository<AssemblyPoint>().AddAsync(entity);
    }

    public async Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<AssemblyPoint>();
        var existingEntity = await repository.GetByPropertyAsync(
            x => x.Id == model.Id,
            tracked: true
        );

        if (existingEntity != null)
        {
            AssemblyPointMapper.UpdateEntity(existingEntity, model);
            await repository.UpdateAsync(existingEntity);
        }
    }

    // REVERTED: Standard Physical Delete
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<AssemblyPoint>().DeleteAsyncById(id);
    }

    public async Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Name == name, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPoint>()
            .GetByPropertyAsync(x => x.Code == code, tracked: false);

        return entity == null ? null : AssemblyPointMapper.ToDomain(entity);
    }

    public async Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<AssemblyPoint>();
        
        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        );

        var domainItems = pagedEntities.Items
            .Select(AssemblyPointMapper.ToDomain)
            .ToList();

        return new PagedResult<AssemblyPointModel>(
            domainItems,
            pagedEntities.TotalCount,
            pagedEntities.PageNumber,
            pagedEntities.PageSize
        );
    }
}
