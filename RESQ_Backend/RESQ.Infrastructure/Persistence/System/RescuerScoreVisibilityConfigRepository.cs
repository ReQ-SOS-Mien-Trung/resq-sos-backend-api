using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.System;

public class RescuerScoreVisibilityConfigRepository(IUnitOfWork unitOfWork) : IRescuerScoreVisibilityConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<RescuerScoreVisibilityConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<RescuerScoreVisibilityConfig>()
            .AsQueryable()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<RescuerScoreVisibilityConfigDto> UpsertAsync(
        int minimumEvaluationCount,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<RescuerScoreVisibilityConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new RescuerScoreVisibilityConfig
            {
                Id = 1,
                MinimumEvaluationCount = minimumEvaluationCount,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };

            await repo.AddAsync(entity);
        }
        else
        {
            entity.MinimumEvaluationCount = minimumEvaluationCount;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();

        return Map(entity);
    }

    private static RescuerScoreVisibilityConfigDto Map(RescuerScoreVisibilityConfig entity)
        => new()
        {
            MinimumEvaluationCount = entity.MinimumEvaluationCount,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}