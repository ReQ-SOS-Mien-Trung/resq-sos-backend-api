using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Resources;
using RESQ.Application.UseCases.Resources.Queries.Depot;
using RESQ.Domain.Entities.Resources;
using RESQ.Infrastructure.Entities;
using RESQ.Infrastructure.Mappers.Resources;

namespace RESQ.Infrastructure.Persistence.Resources;

public class DepotRepository(IUnitOfWork unitOfWork) : IDepotRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        var depot = DepotMapper.ToEntity(depotModel);
        await _unitOfWork.GetRepository<Depot>().AddAsync(depot);
    }

    public async Task<IEnumerable<DepotDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Depot>().GetAllByPropertyAsync();
        return entities.Select(DepotMapper.ToDto) ?? [];
    }

    public async Task<DepotDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Depot>().GetByPropertyAsync(x => x.Id == id);
        if (entity == null)
        {
            return null;
        }
        return DepotMapper.ToDto(entity);
    }
}
