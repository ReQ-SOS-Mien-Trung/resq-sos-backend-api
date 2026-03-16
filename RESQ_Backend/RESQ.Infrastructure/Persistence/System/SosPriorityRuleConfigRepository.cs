using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;
using RESQ.Infrastructure.Entities.System;
using RESQ.Infrastructure.Mappers.System;

namespace RESQ.Infrastructure.Persistence.System;

public class SosPriorityRuleConfigRepository(IUnitOfWork unitOfWork) : ISosPriorityRuleConfigRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosPriorityRuleConfigModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .GetByPropertyAsync(tracked: false);
        return entity == null ? null : SosPriorityRuleConfigMapper.ToDomain(entity);
    }

    public async Task<SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosPriorityRuleConfig>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false);
        return entity == null ? null : SosPriorityRuleConfigMapper.ToDomain(entity);
    }

    public async Task UpdateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default)
    {
        var entity = SosPriorityRuleConfigMapper.ToEntity(model);
        await _unitOfWork.GetRepository<SosPriorityRuleConfig>().UpdateAsync(entity);
    }
}
