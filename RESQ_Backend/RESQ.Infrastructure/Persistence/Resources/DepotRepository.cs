using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Infrastructure.Entities;
using RESQ.Infrastructure.Mappers.Resources;
using RESQ.Infrastructure.Persistence.Base;

namespace RESQ.Infrastructure.Persistence.Resources;

public class DepotRepository(IUnitOfWork unitOfWork, RESQ.Infrastructure.Persistence.Context.ResQDbContext context) : IDepotRepository
{
    private readonly RESQ.Infrastructure.Persistence.Context.ResQDbContext _context = context;

    public async Task CreateAsync(DepotModel depotModel, CancellationToken cancellationToken = default)
    {
        // Mapper now handles creating the DepotManager children entities within the Depot entity
        var depotEntity = DepotMapper.ToEntity(depotModel);
        
        await _context.Depots.AddAsync(depotEntity, cancellationToken);
        
        // No need to manually add DepotManager separate entity here anymore
        // EF Core will insert the graph (Depot + DepotManagers)
    }

    public async Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Depots
            .Include(d => d.DepotManagers) 
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(DepotMapper.ToDomain);
    }

    public async Task<DepotModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Depots
            .Include(d => d.DepotManagers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return DepotMapper.ToDomain(entity);
    }
}
