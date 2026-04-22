using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePromptDraft;

public class CreatePromptDraftCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<CreatePromptDraftCommandHandler> logger) : IRequestHandler<CreatePromptDraftCommand, PromptVersionActionResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<CreatePromptDraftCommandHandler> _logger = logger;

    public async Task<PromptVersionActionResponse> Handle(CreatePromptDraftCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating draft prompt from source Id={SourcePromptId}", request.SourcePromptId);

        var source = await _promptRepository.GetByIdAsync(request.SourcePromptId, cancellationToken);
        if (source == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.SourcePromptId}");
        }

        if (PromptLifecycleStatusResolver.IsDraft(source))
        {
            throw new BadRequestException("Không thể clone draft thành draft mới. Hãy chọn version active hoặc archived.");
        }

        var now = DateTime.UtcNow;
        var versionRoot = PromptLifecycleStatusResolver.ResolveVersionRoot(source.Version);
        var candidateVersion = PromptLifecycleStatusResolver.BuildDraftVersionCandidate(versionRoot, now);
        var suffix = 1;
        while (await _promptRepository.ExistsVersionAsync(source.PromptType, candidateVersion, cancellationToken: cancellationToken))
        {
            candidateVersion = PromptLifecycleStatusResolver.BuildDraftVersionCandidate(versionRoot, now, suffix++);
        }

        var draft = new PromptModel
        {
            Name = source.Name,
            PromptType = source.PromptType,
            Purpose = source.Purpose,
            SystemPrompt = source.SystemPrompt,
            UserPromptTemplate = source.UserPromptTemplate,
            Version = candidateVersion,
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _promptRepository.CreateAsync(draft, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
        {
            EntityId = draft.Id,
            ConfigId = draft.Id,
            EntityType = "Prompt",
            ConfigScope = "Prompt",
            Action = "DraftCreated",
            Status = PromptLifecycleStatusResolver.DetermineStatus(draft),
            ChangedAt = draft.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);

        return new PromptVersionActionResponse
        {
            Id = draft.Id,
            Name = draft.Name,
            PromptType = draft.PromptType,
            Version = draft.Version,
            Status = PromptLifecycleStatusResolver.DetermineStatus(draft),
            Message = "Tạo draft prompt thành công."
        };
    }
}
