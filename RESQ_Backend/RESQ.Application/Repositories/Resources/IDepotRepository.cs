using RESQ.Domain.Entities.Resources;

namespace RESQ.Application.Repositories.Resources
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}
