using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IItemCategoryRepository
{
    Task CreateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(ItemCategoryModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ItemCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ItemCategoryModel?> GetByCodeAsync(ItemCategoryCode code, CancellationToken cancellationToken = default);

    /// <summary>Trả về danh sách Id tương ứng với các code đã cho trong một DB query duy nhất.</summary>
    Task<List<int>> GetIdsByCodesAsync(IReadOnlyList<ItemCategoryCode> codes, CancellationToken cancellationToken = default);

    // Returns all items (e.g. for dropdowns)
    Task<List<ItemCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default);
    
    // Returns paged items (matches DepotRepository pattern)
    Task<PagedResult<ItemCategoryModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
