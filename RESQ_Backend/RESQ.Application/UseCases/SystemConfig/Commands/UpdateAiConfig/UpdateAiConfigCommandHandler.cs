using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Security;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;

public class UpdateAiConfigCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateAiConfigCommandHandler> logger) : IRequestHandler<UpdateAiConfigCommand>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateAiConfigCommandHandler> _logger = logger;

    public async Task Handle(UpdateAiConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating AI config Id={Id}", request.Id);

        var aiConfig = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken);
        if (aiConfig == null)
        {
            throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");
        }

        if (!PromptLifecycleStatusResolver.IsDraft(aiConfig))
        {
            throw new BadRequestException("Chỉ có thể cập nhật draft AI config. Hãy tạo draft mới từ version hiện có.");
        }

        if (request.IsActive == true)
        {
            throw new BadRequestException("Không thể kích hoạt AI config qua endpoint update. Hãy dùng endpoint activate.");
        }

        var normalizedDraftVersion = request.Version?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDraftVersion)
            && !PromptLifecycleStatusResolver.IsDraftVersion(normalizedDraftVersion))
        {
            throw new BadRequestException("Version của draft phải chứa dấu hiệu '-D'.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedDraftVersion))
        {
            var versionExists = await _aiConfigRepository.ExistsVersionAsync(
                normalizedDraftVersion,
                aiConfig.Id,
                cancellationToken);
            if (versionExists)
            {
                throw new ConflictException(
                    $"Đã tồn tại draft AI config khác với version '{normalizedDraftVersion}'.");
            }
        }

        aiConfig.Update(
            name: request.Name?.Trim(),
            provider: request.Provider,
            model: request.Model?.Trim(),
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            apiUrl: request.ApiUrl?.Trim(),
            apiKey: NormalizeUpdatedSecret(request.ApiKey),
            version: string.IsNullOrWhiteSpace(normalizedDraftVersion) ? null : normalizedDraftVersion,
            isActive: false);

        await _aiConfigRepository.UpdateAsync(aiConfig, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Updated AI config successfully: Id={Id}", request.Id);
    }

    private static string? NormalizeUpdatedSecret(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || SecretMasker.IsMasked(trimmed))
        {
            return null;
        }

        return trimmed;
    }
}
