using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<CreatePromptCommandHandler> logger) : IRequestHandler<CreatePromptCommand, CreatePromptResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<CreatePromptCommandHandler> _logger = logger;

    public async Task<CreatePromptResponse> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new prompt with name={Name}, type={Type}", request.Name, request.PromptType);

        var exists = await _promptRepository.ExistsAsync(request.Name, cancellationToken: cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Prompt với tên '{request.Name}' đã tồn tại.");
        }

        if (PromptLifecycleStatusResolver.IsDraftVersion(request.Version))
        {
            throw new BadRequestException("Version tạo mới không được dùng định dạng draft '-D'. Hãy dùng endpoint tạo draft.");
        }

        var normalizedVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(request.Version);
        var versionExists = await _promptRepository.ExistsVersionAsync(
            request.PromptType,
            normalizedVersion,
            cancellationToken: cancellationToken);
        if (versionExists)
        {
            throw new ConflictException(
                $"Prompt type '{request.PromptType}' đã tồn tại version '{normalizedVersion}'.");
        }

        var prompt = PromptModel.Create(
            name: request.Name,
            promptType: request.PromptType,
            purpose: request.Purpose,
            systemPrompt: request.SystemPrompt,
            userPromptTemplate: request.UserPromptTemplate,
            version: normalizedVersion
        );
        prompt.IsActive = request.IsActive;

        await _promptRepository.CreateAsync(prompt, cancellationToken);
        await _unitOfWork.SaveAsync();

        if (prompt.IsActive)
        {
            await _promptRepository.DeactivateOthersByTypeAsync(prompt.Id, prompt.PromptType, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
        {
            EntityId = prompt.Id,
            ConfigId = prompt.Id,
            EntityType = "Prompt",
            ConfigScope = "Prompt",
            Action = "Created",
            Status = prompt.IsActive ? "Active" : "Archived",
            ChangedAt = prompt.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Created prompt successfully: Name={Name}", request.Name);

        return new CreatePromptResponse
        {
            Id = prompt.Id,
            Name = prompt.Name,
            PromptType = prompt.PromptType,
            Message = "Tạo prompt thành công."
        };
    }
}
