using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}
