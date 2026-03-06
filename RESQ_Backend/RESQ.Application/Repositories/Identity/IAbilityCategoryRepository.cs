using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IAbilityCategoryRepository
{
    Task<List<AbilityCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AbilityCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AbilityCategoryModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(AbilityCategoryModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(AbilityCategoryModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
