using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandHandler(
    IPromptRepository promptRepository,
    IAiModelTestService aiModelTestService,
    ILogger<TestPromptCommandHandler> logger) : IRequestHandler<TestPromptCommand, TestPromptResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IAiModelTestService _aiModelTestService = aiModelTestService;
    private readonly ILogger<TestPromptCommandHandler> _logger = logger;

    public async Task<TestPromptResponse> Handle(TestPromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Testing AI model for prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        var result = await _aiModelTestService.TestModelAsync(prompt, cancellationToken);

        return new TestPromptResponse
        {
            IsSuccess = result.IsSuccess,
            PromptId = prompt.Id,
            PromptName = prompt.Name,
            Model = result.Model,
            AiResponse = result.AiResponse,
            ErrorMessage = result.ErrorMessage,
            HttpStatusCode = result.HttpStatusCode,
            ResponseTimeMs = result.ResponseTimeMs
        };
    }
}
