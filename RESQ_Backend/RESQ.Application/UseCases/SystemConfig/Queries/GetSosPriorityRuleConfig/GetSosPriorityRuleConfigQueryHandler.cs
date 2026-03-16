using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public class GetSosPriorityRuleConfigQueryHandler(ISosPriorityRuleConfigRepository repository)
    : IRequestHandler<GetSosPriorityRuleConfigQuery, SosPriorityRuleConfigResponse>,
      IRequestHandler<GetSosPriorityRuleConfigByIdQuery, SosPriorityRuleConfigResponse>
{
    private readonly ISosPriorityRuleConfigRepository _repository = repository;

    public async Task<SosPriorityRuleConfigResponse> Handle(GetSosPriorityRuleConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetAsync(cancellationToken)
            ?? throw new NotFoundException("Chưa có cấu hình quy tắc ưu tiên SOS.");
        return ToResponse(config);
    }

    public async Task<SosPriorityRuleConfigResponse> Handle(GetSosPriorityRuleConfigByIdQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Cấu hình quy tắc ưu tiên SOS với Id={request.Id} không tồn tại.");
        return ToResponse(config);
    }

    private static SosPriorityRuleConfigResponse ToResponse(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config) =>
        new()
        {
            Id = config.Id,
            IssueWeightsJson = config.IssueWeightsJson,
            MedicalSevereIssuesJson = config.MedicalSevereIssuesJson,
            AgeWeightsJson = config.AgeWeightsJson,
            RequestTypeScoresJson = config.RequestTypeScoresJson,
            SituationMultipliersJson = config.SituationMultipliersJson,
            PriorityThresholdsJson = config.PriorityThresholdsJson,
            UpdatedAt = config.UpdatedAt
        };
}
