using RESQ.Domain.Entities.System;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public interface ISosPriorityEvaluationService
{
    Task<SosRuleEvaluationModel> EvaluateAsync(int sosRequestId, string? structuredDataJson, string? sosType, CancellationToken cancellationToken = default);
    Task<SosRuleEvaluationModel> EvaluateWithConfigAsync(
        int sosRequestId,
        string? structuredDataJson,
        string? sosType,
        SosPriorityRuleConfigModel? configModel,
        CancellationToken cancellationToken = default);
}
