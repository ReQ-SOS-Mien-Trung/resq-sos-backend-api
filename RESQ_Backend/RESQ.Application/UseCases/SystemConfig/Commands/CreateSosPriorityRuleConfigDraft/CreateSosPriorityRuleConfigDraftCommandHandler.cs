using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateSosPriorityRuleConfigDraft;

public class CreateSosPriorityRuleConfigDraftCommandHandler(
    ISosPriorityRuleConfigRepository repository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService)
    : IRequestHandler<CreateSosPriorityRuleConfigDraftCommand, SosPriorityRuleConfigResponse>
{
    private readonly ISosPriorityRuleConfigRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task<SosPriorityRuleConfigResponse> Handle(CreateSosPriorityRuleConfigDraftCommand request, CancellationToken cancellationToken)
    {
        var source = request.SourceConfigId.HasValue
            ? await _repository.GetByIdAsync(request.SourceConfigId.Value, cancellationToken)
            : await _repository.GetAsync(cancellationToken);

        if (source == null)
        {
            throw request.SourceConfigId.HasValue
                ? new NotFoundException($"Không tìm thấy cấu hình SOS với Id={request.SourceConfigId.Value} để clone draft.")
                : new NotFoundException("Chưa có cấu hình SOS active để clone draft.");
        }

        var sourceConfig = SosPriorityRuleConfigSupport.FromModel(source);
        sourceConfig.IsActive = false;
        sourceConfig.ConfigVersion = await BuildUniqueDraftVersionAsync(sourceConfig.ConfigVersion, cancellationToken);

        var now = DateTime.UtcNow;
        var draft = new SosPriorityRuleConfigModel
        {
            ConfigVersion = sourceConfig.ConfigVersion,
            IsActive = false,
            CreatedAt = now,
            CreatedBy = request.CreatedBy,
            ActivatedAt = null,
            ActivatedBy = null,
            UpdatedAt = now
        };

        SosPriorityRuleConfigSupport.SyncLegacyFields(draft, sourceConfig);
        await _repository.CreateAsync(draft, cancellationToken);
        await _unitOfWork.SaveAsync();

        var created = (await _repository.GetAllAsync(cancellationToken))
            .FirstOrDefault(x => string.Equals(x.ConfigVersion, draft.ConfigVersion, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Không thể tải draft config vừa tạo.");

        var response = ToResponse(created);

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityId = response.Id,
            EntityType = "SosPriorityRuleConfig",
            ConfigKey = "sos-priority-rule",
            Action = "DraftCreated",
            Status = response.Status,
            ChangedAt = response.UpdatedAt
        }, cancellationToken);

        return response;
    }

    private async Task<string> BuildUniqueDraftVersionAsync(string? baseVersion, CancellationToken cancellationToken)
    {
        var versionRoot = string.IsNullOrWhiteSpace(baseVersion)
            ? "SOS_PRIORITY_V2"
            : baseVersion.Split("_DRAFT_", StringSplitOptions.None)[0].Trim();

        var candidate = $"{versionRoot}_DRAFT_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var suffix = 1;
        while (await _repository.ExistsConfigVersionAsync(candidate, null, cancellationToken))
        {
            candidate = $"{versionRoot}_DRAFT_{DateTime.UtcNow:yyyyMMddHHmmss}_{suffix++}";
        }

        return candidate;
    }

    private static SosPriorityRuleConfigResponse ToResponse(SosPriorityRuleConfigModel config)
    {
        var document = SosPriorityRuleConfigSupport.FromModel(config);
        return new SosPriorityRuleConfigResponse
        {
            Id = config.Id,
            Status = "Draft",
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
            UiOptions = document.UiOptions
        };
    }
}
