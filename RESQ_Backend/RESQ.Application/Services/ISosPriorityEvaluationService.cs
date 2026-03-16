using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public interface ISosPriorityEvaluationService
{
    Task<SosRuleEvaluationModel> EvaluateAsync(int sosRequestId, string? structuredDataJson, string? sosType, CancellationToken cancellationToken = default);
}
