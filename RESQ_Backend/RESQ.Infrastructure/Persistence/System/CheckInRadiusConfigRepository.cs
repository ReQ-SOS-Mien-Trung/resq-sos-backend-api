using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.System;

public class CheckInRadiusConfigRepository(IUnitOfWork unitOfWork) : ICheckInRadiusConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<CheckInRadiusConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<CheckInRadiusConfig>()
            .AsQueryable()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<CheckInRadiusConfigDto> UpsertAsync(
        double maxRadiusMeters,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<CheckInRadiusConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new CheckInRadiusConfig
            {
                Id = 1,
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

    private static CheckInRadiusConfigDto Map(CheckInRadiusConfig entity)
        => new()
        {
            MaxRadiusMeters = entity.MaxRadiusMeters,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}
