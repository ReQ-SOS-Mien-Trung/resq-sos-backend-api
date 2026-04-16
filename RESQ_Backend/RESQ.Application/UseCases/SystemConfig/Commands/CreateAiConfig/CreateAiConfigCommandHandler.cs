using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;

public class CreateAiConfigCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateAiConfigCommandHandler> logger) : IRequestHandler<CreateAiConfigCommand, CreateAiConfigResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateAiConfigCommandHandler> _logger = logger;

    public async Task<CreateAiConfigResponse> Handle(CreateAiConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new AI config with name={Name}", request.Name);

        var exists = await _aiConfigRepository.ExistsAsync(request.Name, cancellationToken: cancellationToken);
        if (exists)
        {
            throw new ConflictException($"AI config voi ten '{request.Name}' da ton tai.");
        }

        if (PromptLifecycleStatusResolver.IsDraftVersion(request.Version))
        {
            throw new BadRequestException("Version tao moi khong duoc dung dinh dang draft '-D'. Hay dung endpoint tao draft.");
        }

        var normalizedVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(request.Version);
        var versionExists = await _aiConfigRepository.ExistsVersionAsync(
            normalizedVersion,
            cancellationToken: cancellationToken);
        if (versionExists)
        {
            throw new ConflictException($"AI config da ton tai version '{normalizedVersion}'.");
        }

        if (request.IsActive && string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new BadRequestException("AI config active phai co api_key.");
        }

        var aiConfig = AiConfigModel.Create(
            name: request.Name.Trim(),
            provider: request.Provider,
            model: request.Model.Trim(),
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            apiUrl: request.ApiUrl.Trim(),
            apiKey: NormalizeSecret(request.ApiKey),
            version: normalizedVersion);
        aiConfig.IsActive = request.IsActive;

        await _aiConfigRepository.CreateAsync(aiConfig, cancellationToken);
        await _unitOfWork.SaveAsync();

        if (aiConfig.IsActive)
        {
            await _aiConfigRepository.DeactivateOthersAsync(aiConfig.Id, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        _logger.LogInformation("Created AI config successfully: Name={Name}", request.Name);

        return new CreateAiConfigResponse
        {
            Id = aiConfig.Id,
            Name = aiConfig.Name,
            Message = "Tao AI config thanh cong."
        };
    }

    private static string? NormalizeSecret(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
