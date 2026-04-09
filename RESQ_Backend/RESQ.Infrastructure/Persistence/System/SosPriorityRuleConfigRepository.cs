using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.System;

public class SosPriorityRuleConfigRepository(IUnitOfWork unitOfWork) : ISosPriorityRuleConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosPriorityRuleConfigModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .AsQueryable()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.ActivatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : SosPriorityRuleConfigMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .AsQueryable()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(SosPriorityRuleConfigMapper.ToDomain).ToList();
    }

    public async Task<SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);
        return entity == null ? null : SosPriorityRuleConfigMapper.ToDomain(entity);
    }

    public async Task<bool> ExistsConfigVersionAsync(string configVersion, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configVersion))
        {
            return false;
        }

        var normalizedConfigVersion = configVersion.Trim().ToLower();
        return await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .AsQueryable()
            .AnyAsync(
                x => x.ConfigVersion.ToLower() == normalizedConfigVersion
                    && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task CreateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default)
    {
        var entity = SosPriorityRuleConfigMapper.ToEntity(model);
        await _unitOfWork.GetRepository<SosPriorityRuleConfig>().AddAsync(entity);
        model.Id = entity.Id;
    }

    public async Task UpdateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default)
    {
        var entity = SosPriorityRuleConfigMapper.ToEntity(model);
        await _unitOfWork.GetRepository<SosPriorityRuleConfig>().UpdateAsync(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.GetRepository<SosPriorityRuleConfig>().DeleteAsyncById(id);
    }
}
