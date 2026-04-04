using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.System;

public class SosClusterGroupingConfigRepository(IUnitOfWork unitOfWork) : ISosClusterGroupingConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosClusterGroupingConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosClusterGroupingConfig>()
            .AsQueryable()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<SosClusterGroupingConfigDto> UpsertAsync(
        double maximumDistanceKm,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<SosClusterGroupingConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new SosClusterGroupingConfig
            {
                Id = 1,
                MaximumDistanceKm = maximumDistanceKm,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };

            await repo.AddAsync(entity);
        }
        else
        {
            entity.MaximumDistanceKm = maximumDistanceKm;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();

        return Map(entity);
    }

    private static SosClusterGroupingConfigDto Map(SosClusterGroupingConfig entity)
        => new()
        {
            MaximumDistanceKm = entity.MaximumDistanceKm,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}