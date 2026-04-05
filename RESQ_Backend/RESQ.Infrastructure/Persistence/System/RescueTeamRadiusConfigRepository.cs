using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.System;

public class RescueTeamRadiusConfigRepository(IUnitOfWork unitOfWork) : IRescueTeamRadiusConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<RescueTeamRadiusConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<RescueTeamRadiusConfig>()
            .AsQueryable()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<RescueTeamRadiusConfigDto> UpsertAsync(
        double maxRadiusKm,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<RescueTeamRadiusConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new RescueTeamRadiusConfig
            {
                Id = 1,
                MaxRadiusKm = maxRadiusKm,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };

            await repo.AddAsync(entity);
        }
        else
        {
            entity.MaxRadiusKm = maxRadiusKm;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();

        return Map(entity);
    }

    private static RescueTeamRadiusConfigDto Map(RescueTeamRadiusConfig entity)
        => new()
        {
            MaxRadiusKm = entity.MaxRadiusKm,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}
