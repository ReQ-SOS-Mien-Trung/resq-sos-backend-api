using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services;

public class SosAiAnalysisService : ISosAiAnalysisService
{
    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly IAiConfigRepository _aiConfigRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly ISosAiAnalysisRepository _sosAiAnalysisRepository;
    private readonly ISosRequestRepository _sosRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SosAiAnalysisService> _logger;

    // Fallback defaults - chỉ dùng khi database chưa có cấu hình
    public SosAiAnalysisService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        IAiConfigRepository aiConfigRepository,
        IPromptRepository promptRepository,
        ISosAiAnalysisRepository sosAiAnalysisRepository,
        ISosRequestRepository sosRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<SosAiAnalysisService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _aiConfigRepository = aiConfigRepository;
        _promptRepository = promptRepository;
        _sosAiAnalysisRepository = sosAiAnalysisRepository;
        _sosRequestRepository = sosRequestRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task AnalyzeAndSaveAsync(int sosRequestId, string? structuredData, string? rawMessage, string? sosType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting AI analysis for SOS Request Id={sosRequestId}", sosRequestId);

            // Get prompt configuration from database theo PromptType
            var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.SosPriorityAnalysis, cancellationToken);
            if (prompt == null)
            {
                _logger.LogWarning("Không tìm thấy prompt đang active cho loại SosPriorityAnalysis. Bỏ qua AI analysis.");
                return;
            }

            var aiConfig = await _aiConfigRepository.GetActiveAsync(cancellationToken);
            if (aiConfig == null)
            {
                _logger.LogWarning("Khong tim thay AI config active. Bo qua AI analysis cho SOS Request Id={sosRequestId}.", sosRequestId);
                return;
            }

            var settings = _settingsResolver.Resolve(aiConfig);

            _logger.LogInformation(
                "Using AI config from DB: Provider={provider}, Model={model}, ApiUrl={apiUrl}, Temperature={temperature}, MaxTokens={maxTokens}",
                settings.Provider, settings.Model, settings.ApiUrl, settings.Temperature, settings.MaxTokens);

            // Build user prompt from template
            var userPrompt = BuildUserPrompt(prompt.UserPromptTemplate, structuredData, rawMessage, sosType);

            // Call AI API using database configuration
            var aiResponse = await CallAiApiAsync(settings, prompt.SystemPrompt, userPrompt, cancellationToken);

            if (aiResponse == null)
            {
                _logger.LogWarning("AI response is null for SOS Request Id={sosRequestId}", sosRequestId);
                return;
            }

            // Parse AI response
            var analysisResult = ParseAiResponse(aiResponse);

            // Create and save analysis
            var analysis = SosAiAnalysisModel.Create(
                sosRequestId: sosRequestId,
                modelName: settings.Model,
                modelVersion: prompt.Version,
                analysisType: "PRIORITY_ANALYSIS",
                suggestedSeverityLevel: analysisResult.SeverityLevel,
                suggestedPriority: analysisResult.Priority,
                explanation: analysisResult.Explanation,
                confidenceScore: analysisResult.ConfidenceScore,
                suggestionScope: "SOS_REQUEST",
                metadata: JsonSerializer.Serialize(new
                {
                    rawResponse = aiResponse,
                    analysisResult,
                    promptId = prompt.Id,
                    promptVersion = prompt.Version,
                    provider = settings.Provider.ToString()
                })
            );

            await _sosAiAnalysisRepository.CreateAsync(analysis, cancellationToken);

            // Update SOS request with AI suggested priority if not already set
            var sosRequest = await _sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
            if (sosRequest != null && sosRequest.PriorityLevel == null)
            {
                if (Enum.TryParse<SosPriorityLevel>(analysisResult.Priority, true, out var aiPriority))
                {
                    sosRequest.PriorityLevel = aiPriority;
                    await _sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);
                }
            }

            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "AI analysis completed for SOS Request Id={sosRequestId}: Provider={provider}, Model={model}, Priority={priority}, Severity={severity}, Confidence={confidence}",
                sosRequestId, settings.Provider, settings.Model, analysisResult.Priority, analysisResult.SeverityLevel, analysisResult.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI analysis for SOS Request Id={sosRequestId}", sosRequestId);
        }
    }

    private static string BuildUserPrompt(string? template, string? structuredData, string? rawMessage, string? sosType)
    {
        if (string.IsNullOrEmpty(template))
        {
            return $"Analyze this SOS request:\nType: {sosType}\nMessage: {rawMessage}\nStructured Data: {structuredData}";
        }

        return template
            .Replace("{{structured_data}}", structuredData ?? "N/A")
            .Replace("{{raw_message}}", rawMessage ?? "N/A")
            .Replace("{{sos_type}}", sosType ?? "UNKNOWN");
    }

    /// <summary>
    /// Gọi AI API sử dụng cấu hình từ database (model, url, temperature, maxTokens).
    /// </summary>
    private async Task<string?> CallAiApiAsync(
        AiPromptExecutionSettings settings,
        string? systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _aiProviderClientFactory
                .GetClient(settings.Provider)
                .CompleteAsync(new AiCompletionRequest
                {
                    Provider = settings.Provider,
                    Model = settings.Model,
                    ApiUrl = settings.ApiUrl,
                    ApiKey = settings.ApiKey,
                    SystemPrompt = systemPrompt,
                    Temperature = settings.Temperature,
                    MaxTokens = settings.MaxTokens,
                    Timeout = TimeSpan.FromSeconds(30),
                    Messages = [AiChatMessage.User(userPrompt)]
                }, cancellationToken);

            if (response.HttpStatusCode is >= 400)
            {
                _logger.LogError(
                    "AI API error: Provider={provider}, StatusCode={statusCode}, Error={error}, Model={model}, ApiUrl={url}",
                    settings.Provider,
                    response.HttpStatusCode,
                    response.ErrorBody,
                    settings.Model,
                    settings.ApiUrl);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(response.BlockReason))
            {
                _logger.LogWarning(
                    "AI provider blocked SOS analysis: Provider={provider}, BlockReason={reason}, Model={model}",
                    settings.Provider,
                    response.BlockReason,
                    settings.Model);
                return null;
            }

            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI API (Provider={provider}, Model={model}, URL={url})", settings.Provider, settings.Model, settings.ApiUrl);
            return null;
        }
    }

    private static AiAnalysisResult ParseAiResponse(string response)
    {
        try
        {
            // Try to parse JSON response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AiAnalysisResult>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                    return parsed;
            }
        }
        catch
        {
            // If JSON parsing fails, extract from text
        }

        // Default fallback - extract from text
        return new AiAnalysisResult
        {
            Priority = ExtractPriority(response),
            SeverityLevel = ExtractSeverity(response),
            Explanation = response.Length > 500 ? response[..500] : response,
            ConfidenceScore = 0.5
        };
    }

    private static string ExtractPriority(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("CRITICAL")) return "Critical";
        if (upper.Contains("HIGH")) return "High";
        if (upper.Contains("MEDIUM")) return "Medium";
        return "Low";
    }

    private static string ExtractSeverity(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("LIFE-THREATENING") || upper.Contains("CRITICAL")) return "Critical";
        if (upper.Contains("SEVERE") || upper.Contains("HIGH")) return "Severe";
        if (upper.Contains("MODERATE") || upper.Contains("MEDIUM")) return "Moderate";
        return "Minor";
    }
    private class AiAnalysisResult
    {
        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "Medium";

        [JsonPropertyName("severity_level")]
        public string SeverityLevel { get; set; } = "Moderate";

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [JsonPropertyName("confidence_score")]
        public double ConfidenceScore { get; set; } = 0.5;
    }
}
