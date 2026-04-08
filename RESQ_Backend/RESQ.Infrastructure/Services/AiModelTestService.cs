using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;

namespace RESQ.Infrastructure.Services;

public class AiModelTestService : IAiModelTestService
{
    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly ILogger<AiModelTestService> _logger;

    private const string TEST_PROMPT = "Respond with exactly: {\"status\": \"ok\", \"message\": \"Model is working\"}";
    private const string FALLBACK_GEMINI_MODEL = "gemini-2.5-flash";
    private const string FALLBACK_GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const double FALLBACK_TEMPERATURE = 0.3;
    private const int FALLBACK_MAX_TOKENS = 256;

    public AiModelTestService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        ILogger<AiModelTestService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _logger = logger;
    }

    public async Task<AiModelTestResult> TestModelAsync(
        PromptModel prompt,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = _settingsResolver.Resolve(
            prompt,
            new AiPromptExecutionFallback(
                FALLBACK_GEMINI_MODEL,
                FALLBACK_GEMINI_API_URL,
                FALLBACK_TEMPERATURE,
                FALLBACK_MAX_TOKENS));

        try
        {
            _logger.LogInformation(
                "Testing AI model: Provider={provider}, Model={model}, ApiUrl={apiUrl}",
                settings.Provider,
                settings.Model,
                settings.ApiUrl);

            var request = new AiCompletionRequest
            {
                Provider = settings.Provider,
                Model = settings.Model,
                ApiUrl = settings.ApiUrl,
                ApiKey = settings.ApiKey,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens,
                Timeout = TimeSpan.FromSeconds(30),
                Messages = [AiChatMessage.User(TEST_PROMPT)]
            };

            var response = await _aiProviderClientFactory
                .GetClient(settings.Provider)
                .CompleteAsync(request, cancellationToken);

            stopwatch.Stop();

            if (response.HttpStatusCode is >= 400)
            {
                _logger.LogWarning(
                    "AI model test failed: Provider={provider}, Model={model}, StatusCode={statusCode}",
                    settings.Provider,
                    settings.Model,
                    response.HttpStatusCode);

                return new AiModelTestResult
                {
                    IsSuccess = false,
                    Model = settings.Model,
                    ErrorMessage = $"API trả về lỗi: {response.HttpStatusCode}. Chi tiết: {Truncate(response.ErrorBody, 500)}",
                    HttpStatusCode = response.HttpStatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            if (!string.IsNullOrWhiteSpace(response.BlockReason))
            {
                return new AiModelTestResult
                {
                    IsSuccess = false,
                    Model = settings.Model,
                    ErrorMessage = $"Yêu cầu test bị chặn bởi provider: {response.BlockReason}",
                    HttpStatusCode = response.HttpStatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation(
                "AI model test succeeded: Provider={provider}, Model={model}, ResponseTime={ms}ms",
                settings.Provider,
                settings.Model,
                stopwatch.ElapsedMilliseconds);

            return new AiModelTestResult
            {
                IsSuccess = true,
                Model = settings.Model,
                AiResponse = Truncate(response.Text, 500),
                HttpStatusCode = response.HttpStatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new AiModelTestResult
            {
                IsSuccess = false,
                Model = settings.Model,
                ErrorMessage = "Request timeout - AI API không phản hồi trong 30 giây.",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new AiModelTestResult
            {
                IsSuccess = false,
                Model = settings.Model,
                ErrorMessage = $"Không thể kết nối đến AI API: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error testing AI model {model}", settings.Model);
            return new AiModelTestResult
            {
                IsSuccess = false,
                Model = settings.Model,
                ErrorMessage = $"Lỗi không xác định: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (text == null) return null;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
