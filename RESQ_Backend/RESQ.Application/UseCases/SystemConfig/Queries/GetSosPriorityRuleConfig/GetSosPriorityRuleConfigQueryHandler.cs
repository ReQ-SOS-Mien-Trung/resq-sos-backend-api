using MediatR;
using RESQ.Application.Common;
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
        CreateResponse(config);

    private static SosPriorityRuleConfigResponse CreateResponse(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config)
    {
        var document = SosPriorityRuleConfigSupport.FromModel(config);
        return new SosPriorityRuleConfigResponse
        {
            Id = config.Id,
            UpdatedAt = config.UpdatedAt,
            ConfigVersion = document.ConfigVersion,
            IsActive = document.IsActive,
            PriorityScore = document.PriorityScore,
            MedicalScore = document.MedicalScore,
            ReliefScore = document.ReliefScore,
            SituationMultiplier = document.SituationMultiplier,
            PriorityLevel = document.PriorityLevel,
            UiConstraints = document.UiConstraints,
            UiOptions = document.UiOptions
        };
    }
}
