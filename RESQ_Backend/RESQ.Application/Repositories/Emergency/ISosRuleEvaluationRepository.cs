using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosRuleEvaluationRepository
{
    Task CreateAsync(SosRuleEvaluationModel evaluation, CancellationToken cancellationToken = default);
    Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default);
}
