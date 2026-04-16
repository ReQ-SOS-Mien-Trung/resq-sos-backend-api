using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Security;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdatePrompt;

public class UpdatePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdatePromptCommandHandler> logger) : IRequestHandler<UpdatePromptCommand>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdatePromptCommandHandler> _logger = logger;

    public async Task Handle(UpdatePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        // Check duplicate name if name is being changed
        if (!string.IsNullOrEmpty(request.Name) && !string.Equals(prompt.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _promptRepository.ExistsAsync(request.Name, cancellationToken);
            if (exists)
            {
                throw new ConflictException($"Prompt với tên '{request.Name}' đã tồn tại.");
            }
        }

        prompt.Update(
            name: request.Name,
            promptType: request.PromptType,
            provider: request.Provider,
            purpose: request.Purpose,
            systemPrompt: request.SystemPrompt,
            userPromptTemplate: request.UserPromptTemplate,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            version: request.Version,
            apiUrl: request.ApiUrl,
            apiKey: NormalizeApiKeyForUpdate(request.ApiKey),
            isActive: request.IsActive
        );

        await _promptRepository.UpdateAsync(prompt, cancellationToken);

        // Nếu prompt được kích hoạt, tắt các prompt khác cùng loại
        if (request.IsActive == true)
        {
            await _promptRepository.DeactivateOthersByTypeAsync(prompt.Id, prompt.PromptType, cancellationToken);
        }

        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Updated prompt successfully: Id={Id}", request.Id);
    }

    private static string? NormalizeApiKeyForUpdate(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || SecretMasker.IsMasked(apiKey))
        {
            return null;
        }

        return apiKey;
    }
}
