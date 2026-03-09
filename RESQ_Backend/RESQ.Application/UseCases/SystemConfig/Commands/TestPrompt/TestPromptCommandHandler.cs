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

    private const string FALLBACK_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    public async Task<TestPromptResponse> Handle(TestPromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Testing AI model for prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        var model = prompt.Model ?? "gemini-2.5-flash";
        var apiUrl = prompt.ApiUrl ?? FALLBACK_API_URL;
        var apiKey = prompt.ApiKey ?? string.Empty;
        var temperature = prompt.Temperature ?? 0.3;
        var maxTokens = prompt.MaxTokens ?? 256; // Dùng ít token cho test

        var result = await _aiModelTestService.TestModelAsync(model, apiUrl, apiKey, temperature, maxTokens, cancellationToken);

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
