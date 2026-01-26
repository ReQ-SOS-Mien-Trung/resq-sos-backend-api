using RESQ.Application.UseCases.Resources.Queries.Depot;
using RESQ.Domain.Entities.Resources;

namespace RESQ.Application.Repositories.Resources
{
    public interface IDepotRepository 
    {
        Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default);
        Task<IEnumerable<DepotDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DepotDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}

