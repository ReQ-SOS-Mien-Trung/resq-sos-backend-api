using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfigDraft;

public class CreateAiConfigDraftCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<CreateAiConfigDraftCommandHandler> logger) : IRequestHandler<CreateAiConfigDraftCommand, AiConfigVersionActionResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<CreateAiConfigDraftCommandHandler> _logger = logger;

    public async Task<AiConfigVersionActionResponse> Handle(CreateAiConfigDraftCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating draft AI config from source Id={SourceAiConfigId}", request.SourceAiConfigId);

        var source = await _aiConfigRepository.GetByIdAsync(request.SourceAiConfigId, cancellationToken);
        if (source == null)
        {
            throw new NotFoundException($"Không tìm thấy AI config với Id={request.SourceAiConfigId}");
        }

        if (PromptLifecycleStatusResolver.IsDraft(source))
        {
            throw new BadRequestException("Không thể clone draft thành draft mới. Hãy chọn version active hoặc archived.");
        }

        var now = DateTime.UtcNow;
        var versionRoot = PromptLifecycleStatusResolver.ResolveVersionRoot(source.Version);
        var candidateVersion = PromptLifecycleStatusResolver.BuildDraftVersionCandidate(versionRoot, now);
        var suffix = 1;
        while (await _aiConfigRepository.ExistsVersionAsync(candidateVersion, cancellationToken: cancellationToken))
        {
            candidateVersion = PromptLifecycleStatusResolver.BuildDraftVersionCandidate(versionRoot, now, suffix++);
        }

        var draft = new AiConfigModel
        {
            Name = source.Name,
            Provider = source.Provider,
            Model = source.Model,
            Temperature = source.Temperature,
            MaxTokens = source.MaxTokens,
            ApiUrl = AiProviderDefaults.ResolveApiUrl(source.Provider),
            ApiKey = source.ApiKey,
            Version = candidateVersion,
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _aiConfigRepository.CreateAsync(draft, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
        {
            EntityId = draft.Id,
            ConfigId = draft.Id,
            EntityType = "AiConfig",
            ConfigScope = "AiConfig",
            Action = "DraftCreated",
            Status = PromptLifecycleStatusResolver.DetermineStatus(draft),
            ChangedAt = draft.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);

        return new AiConfigVersionActionResponse
        {
            Id = draft.Id,
            Name = draft.Name,
            Version = draft.Version,
            Status = PromptLifecycleStatusResolver.DetermineStatus(draft),
            Message = "Tạo draft AI config thành công."
        };
    }
}
