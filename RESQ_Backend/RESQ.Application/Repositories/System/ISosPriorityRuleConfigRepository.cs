using RESQ.Domain.Entities.System;

namespace RESQ.Application.Repositories.System;

public interface ISosPriorityRuleConfigRepository
{
    Task<SosPriorityRuleConfigModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SosPriorityRuleConfigModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SosPriorityRuleConfigModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsConfigVersionAsync(string configVersion, int? excludeId = null, CancellationToken cancellationToken = default);
    Task CreateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(SosPriorityRuleConfigModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
