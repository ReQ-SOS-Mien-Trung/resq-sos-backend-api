using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task UpdateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        
        // NEW: Pagination
        Task<PagedResult<DepotModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        
        // Legacy GetAll (optional, can be kept or removed)
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);
        
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}
