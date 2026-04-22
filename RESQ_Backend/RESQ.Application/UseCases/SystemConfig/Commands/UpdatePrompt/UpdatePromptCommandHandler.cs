using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<UpdatePromptCommandHandler> logger) : IRequestHandler<UpdatePromptCommand>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<UpdatePromptCommandHandler> _logger = logger;

    public async Task Handle(UpdatePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        if (!PromptLifecycleStatusResolver.IsDraft(prompt))
        {
            throw new BadRequestException("Chỉ có thể cập nhật draft prompt. Hãy tạo draft mới từ version hiện có.");
        }

        if (request.IsActive == true)
        {
            throw new BadRequestException("Không thể kích hoạt prompt qua endpoint update. Hãy dùng endpoint activate.");
        }

        if (request.PromptType.HasValue && request.PromptType.Value != prompt.PromptType)
        {
            throw new BadRequestException("Không thể đổi PromptType của draft prompt. Hãy tạo draft mới cho loại prompt khác.");
        }

        var normalizedDraftVersion = request.Version?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedDraftVersion)
            && !PromptLifecycleStatusResolver.IsDraftVersion(normalizedDraftVersion))
        {
            throw new BadRequestException("Version của draft phải chứa dấu hiệu '-D'.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedDraftVersion))
        {
            var versionExists = await _promptRepository.ExistsVersionAsync(
                prompt.PromptType,
                normalizedDraftVersion,
                prompt.Id,
                cancellationToken);
            if (versionExists)
            {
                throw new ConflictException(
                    $"Đã tồn tại draft prompt khác của type '{prompt.PromptType}' với version '{normalizedDraftVersion}'.");
            }
        }

        prompt.Update(
            name: request.Name,
            promptType: null,
            purpose: request.Purpose,
            systemPrompt: request.SystemPrompt,
            userPromptTemplate: request.UserPromptTemplate,
            version: normalizedDraftVersion,
            isActive: false);

        await _promptRepository.UpdateAsync(prompt, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
        {
            EntityId = prompt.Id,
            ConfigId = prompt.Id,
            EntityType = "Prompt",
            ConfigScope = "Prompt",
            Action = "Updated",
            Status = "Draft",
            ChangedAt = prompt.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Updated prompt successfully: Id={Id}", request.Id);
    }
}
