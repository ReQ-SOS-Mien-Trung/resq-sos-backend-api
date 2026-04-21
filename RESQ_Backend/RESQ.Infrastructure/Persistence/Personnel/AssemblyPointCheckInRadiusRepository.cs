using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class AssemblyPointCheckInRadiusRepository(IUnitOfWork unitOfWork) : IAssemblyPointCheckInRadiusRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<AssemblyPointCheckInRadiusConfigDto?> GetByAssemblyPointIdAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<AssemblyPointCheckInRadiusConfig>()
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.AssemblyPointId == assemblyPointId, cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<List<AssemblyPointCheckInRadiusConfigDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<AssemblyPointCheckInRadiusConfig>()
            .AsQueryable()
            .OrderBy(x => x.AssemblyPointId)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public async Task<AssemblyPointCheckInRadiusConfigDto> UpsertAsync(
        int assemblyPointId,
        double maxRadiusMeters,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<AssemblyPointCheckInRadiusConfig>();
        var entity = await repo
            .AsQueryable(tracked: true)
            .FirstOrDefaultAsync(x => x.AssemblyPointId == assemblyPointId, cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new AssemblyPointCheckInRadiusConfig
            {
                AssemblyPointId = assemblyPointId,
                MaxRadiusMeters = maxRadiusMeters,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };
            await repo.AddAsync(entity);
        }
        else
        {
            entity.MaxRadiusMeters = maxRadiusMeters;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();
        return Map(entity);
    }

    public async Task<bool> DeleteByAssemblyPointIdAsync(
        int assemblyPointId,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<AssemblyPointCheckInRadiusConfig>();
        var entity = await repo
            .AsQueryable(tracked: true)
            .FirstOrDefaultAsync(x => x.AssemblyPointId == assemblyPointId, cancellationToken);

        if (entity == null)
            return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveAsync();
        return true;
    }

    private static AssemblyPointCheckInRadiusConfigDto Map(AssemblyPointCheckInRadiusConfig entity)
        => new()
        {
            Id = entity.Id,
            AssemblyPointId = entity.AssemblyPointId,
            MaxRadiusMeters = entity.MaxRadiusMeters,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}
