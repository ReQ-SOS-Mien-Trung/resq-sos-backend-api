using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public class GetSosPriorityRuleConfigQueryHandler(ISosPriorityRuleConfigRepository repository)
    : IRequestHandler<GetSosPriorityRuleConfigQuery, SosPriorityRuleConfigResponse>,
      IRequestHandler<GetSosPriorityRuleConfigByIdQuery, SosPriorityRuleConfigResponse>,
      IRequestHandler<GetSosPriorityRuleConfigVersionsQuery, List<SosPriorityRuleConfigVersionSummaryResponse>>
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

    public async Task<List<SosPriorityRuleConfigVersionSummaryResponse>> Handle(GetSosPriorityRuleConfigVersionsQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync(cancellationToken);
        return configs.Select(ToVersionSummary).ToList();
    }

    private static SosPriorityRuleConfigResponse ToResponse(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config) =>
        CreateResponse(config);

    private static SosPriorityRuleConfigResponse CreateResponse(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config)
    {
        var document = SosPriorityRuleConfigSupport.FromModel(config);
        return new SosPriorityRuleConfigResponse
        {
            Id = config.Id,
            Status = DetermineStatus(config),
            CreatedAt = config.CreatedAt,
            CreatedBy = config.CreatedBy,
            ActivatedAt = config.ActivatedAt,
            ActivatedBy = config.ActivatedBy,
            UpdatedAt = config.UpdatedAt,
            ConfigVersion = document.ConfigVersion,
            IsActive = document.IsActive,
            MedicalSevereIssues = document.MedicalSevereIssues,
            RequestTypeScores = document.RequestTypeScores,
            PriorityScore = document.PriorityScore,
            MedicalScore = document.MedicalScore,
            ReliefScore = document.ReliefScore,
            SituationMultiplier = document.SituationMultiplier,
            PriorityLevel = document.PriorityLevel,
            UiConstraints = document.UiConstraints,
            UiOptions = document.UiOptions,
            DisplayLabels = document.DisplayLabels
        };
    }

    private static SosPriorityRuleConfigVersionSummaryResponse ToVersionSummary(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config)
    {
        return new SosPriorityRuleConfigVersionSummaryResponse
        {
            Id = config.Id,
            ConfigVersion = string.IsNullOrWhiteSpace(config.ConfigVersion) ? $"CONFIG_{config.Id}" : config.ConfigVersion,
            IsActive = config.IsActive,
            Status = DetermineStatus(config),
            CreatedAt = config.CreatedAt,
            CreatedBy = config.CreatedBy,
            ActivatedAt = config.ActivatedAt,
            ActivatedBy = config.ActivatedBy,
            UpdatedAt = config.UpdatedAt
        };
    }

    private static string DetermineStatus(RESQ.Domain.Entities.System.SosPriorityRuleConfigModel config)
    {
        if (config.IsActive)
        {
            return "Active";
        }

        return config.ActivatedAt.HasValue ? "Archived" : "Draft";
    }
}
