using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePrompt;

public class CreatePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreatePromptCommandHandler> logger) : IRequestHandler<CreatePromptCommand, CreatePromptResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreatePromptCommandHandler> _logger = logger;

    public async Task<CreatePromptResponse> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new prompt with name={Name}, type={Type}", request.Name, request.PromptType);

        // Check duplicate name
        var exists = await _promptRepository.ExistsAsync(request.Name, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Prompt với tên '{request.Name}' đã tồn tại.");
        }

        var prompt = PromptModel.Create(
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
            apiKey: request.ApiKey
        );
        prompt.IsActive = request.IsActive;

        await _promptRepository.CreateAsync(prompt, cancellationToken);
        await _unitOfWork.SaveAsync();

        // Nếu prompt này được kích hoạt, tắt các prompt khác cùng loại
        if (prompt.IsActive)
        {
            await _promptRepository.DeactivateOthersByTypeAsync(prompt.Id, prompt.PromptType, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

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
