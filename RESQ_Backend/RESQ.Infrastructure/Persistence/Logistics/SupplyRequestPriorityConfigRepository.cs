using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Persistence.Logistics;

public class SupplyRequestPriorityConfigRepository(IUnitOfWork unitOfWork) : ISupplyRequestPriorityConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SupplyRequestPriorityConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SupplyRequestPriorityConfig>()
            .AsQueryable()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task<SupplyRequestPriorityConfigDto> UpsertAsync(
        int urgentMinutes,
        int highMinutes,
        int mediumMinutes,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.GetRepository<SupplyRequestPriorityConfig>();
        var entity = await repo.AsQueryable(tracked: true)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (entity == null)
        {
            entity = new SupplyRequestPriorityConfig
            {
                Id = 1,
                UrgentMinutes = urgentMinutes,
                HighMinutes = highMinutes,
                MediumMinutes = mediumMinutes,
                UpdatedBy = updatedBy,
                UpdatedAt = now
            };

            await repo.AddAsync(entity);
        }
        else
        {
            entity.UrgentMinutes = urgentMinutes;
            entity.HighMinutes = highMinutes;
            entity.MediumMinutes = mediumMinutes;
            entity.UpdatedBy = updatedBy;
            entity.UpdatedAt = now;
        }

        await _unitOfWork.SaveAsync();
        return Map(entity);
    }

    private static SupplyRequestPriorityConfigDto Map(SupplyRequestPriorityConfig entity)
        => new()
        {
            UrgentMinutes = entity.UrgentMinutes,
            HighMinutes = entity.HighMinutes,
            MediumMinutes = entity.MediumMinutes,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
}
