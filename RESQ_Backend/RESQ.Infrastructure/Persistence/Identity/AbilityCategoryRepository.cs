using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Identity;

public class AbilityCategoryRepository(IUnitOfWork unitOfWork) : IAbilityCategoryRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<AbilityCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<AbilityCategory>()
            .GetAllByPropertyAsync();
        return entities.Select(MapToModel).OrderBy(x => x.Id).ToList();
    }

    public async Task<AbilityCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AbilityCategory>()
            .GetByPropertyAsync(x => x.Id == id);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<AbilityCategoryModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AbilityCategory>()
            .GetByPropertyAsync(x => x.Code == code);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<int> CreateAsync(AbilityCategoryModel model, CancellationToken cancellationToken = default)
    {
        var entity = new AbilityCategory
        {
            Code = model.Code,
            Description = model.Description
        };
        await _unitOfWork.GetRepository<AbilityCategory>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task UpdateAsync(AbilityCategoryModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AbilityCategory>()
            .GetByPropertyAsync(x => x.Id == model.Id);
        if (entity is not null)
        {
            entity.Code = model.Code;
            entity.Description = model.Description;
            await _unitOfWork.GetRepository<AbilityCategory>().UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<AbilityCategory>().DeleteAsyncById(id);
    }

    private static AbilityCategoryModel MapToModel(AbilityCategory entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Description = entity.Description
    };
}
