using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public interface ISosPriorityEvaluationService
{
    SosRuleEvaluationModel Evaluate(int sosRequestId, string? structuredDataJson, string? sosType);
}
