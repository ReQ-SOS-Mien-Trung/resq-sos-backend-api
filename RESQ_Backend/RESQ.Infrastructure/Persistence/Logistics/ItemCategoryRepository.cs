using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Mappers.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class ItemCategoryRepository(IUnitOfWork unitOfWork) : IItemCategoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default)
    {
        var entity = ItemCategoryMapper.ToEntity(model);
        await _unitOfWork.GetRepository<Category>().AddAsync(entity);
    }

    public async Task UpdateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Category>();
        var existingEntity = await repository.GetByPropertyAsync(x => x.Id == model.Id);

        if (existingEntity != null)
        {
            ItemCategoryMapper.UpdateEntity(existingEntity, model);
            await repository.UpdateAsync(existingEntity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<Category>().DeleteAsyncById(id);
    }

    public async Task<ItemCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Category>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);

        return entity == null ? null : ItemCategoryMapper.ToDomain(entity);
    }

    public async Task<ItemCategoryModel?> GetByCodeAsync(ItemCategoryCode code, CancellationToken cancellationToken = default)
    {
        var codeString = code.ToString().ToLower();
        var entity = await _unitOfWork.GetRepository<Category>()
            .GetByPropertyAsync(x => x.Code.ToLower() == codeString, tracked: false);

        return entity == null ? null : ItemCategoryMapper.ToDomain(entity);
    }

    public async Task<List<int>> GetIdsByCodesAsync(IReadOnlyList<ItemCategoryCode> codes, CancellationToken cancellationToken = default)
    {
        if (codes == null || codes.Count == 0)
            return [];

        var codeStrings = codes.Select(c => c.ToString().ToLower()).Distinct().ToList();

        return await _unitOfWork.Set<Category>()
            .AsNoTracking()
            .Where(c => codeStrings.Contains(c.Code.ToLower()))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    // Original implementation restored for non-paged access
    public async Task<List<ItemCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Category>().GetAllByPropertyAsync();
        return entities.OrderBy(c => c.Id).Select(ItemCategoryMapper.ToDomain).ToList();
    }

    // New implementation for paged access (matches DepotRepository)
    public async Task<PagedResult<ItemCategoryModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.GetRepository<Category>();

        var pagedEntities = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            filter: null,
            orderBy: q => q.OrderByDescending(c => c.CreatedAt)
        );

        var domainItems = pagedEntities.Items
            .Select(ItemCategoryMapper.ToDomain)
            .ToList();

        return new PagedResult<ItemCategoryModel>(
            domainItems,
            pagedEntities.TotalCount,
            pagedEntities.PageNumber,
            pagedEntities.PageSize
        );
    }
}
