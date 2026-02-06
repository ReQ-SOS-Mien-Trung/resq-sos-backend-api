using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Infrastructure.Services;

public class SosAiAnalysisService : ISosAiAnalysisService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPromptRepository _promptRepository;
    private readonly ISosAiAnalysisRepository _sosAiAnalysisRepository;
    private readonly ISosRequestRepository _sosRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SosAiAnalysisService> _logger;
    private readonly string _apiKey;

    private const string PROMPT_NAME = "SOS_PRIORITY_ANALYSIS";
    private const string DEFAULT_MODEL = "gemini-2.5-flash";
    private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    public SosAiAnalysisService(
        IHttpClientFactory httpClientFactory,
        IPromptRepository promptRepository,
        ISosAiAnalysisRepository sosAiAnalysisRepository,
        ISosRequestRepository sosRequestRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SosAiAnalysisService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _promptRepository = promptRepository;
        _sosAiAnalysisRepository = sosAiAnalysisRepository;
        _sosRequestRepository = sosRequestRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task AnalyzeAndSaveAsync(int sosRequestId, string? structuredData, string? rawMessage, string? sosType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting AI analysis for SOS Request Id={sosRequestId}", sosRequestId);

            // Get prompt from database
            var prompt = await _promptRepository.GetByNameAsync(PROMPT_NAME, cancellationToken);
            if (prompt == null)
            {
                _logger.LogWarning("Prompt '{promptName}' not found. Skipping AI analysis.", PROMPT_NAME);
                return;
            }

            // Build user prompt from template
            var userPrompt = BuildUserPrompt(prompt.UserPromptTemplate, structuredData, rawMessage, sosType);

            // Call Gemini API
            var aiResponse = await CallGeminiApiAsync(prompt.Model ?? DEFAULT_MODEL, prompt.SystemPrompt, userPrompt, prompt.Temperature ?? 0.3, cancellationToken);

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
                modelName: prompt.Model ?? DEFAULT_MODEL,
                modelVersion: prompt.Version,
                analysisType: "PRIORITY_ANALYSIS",
                suggestedSeverityLevel: analysisResult.SeverityLevel,
                suggestedPriority: analysisResult.Priority,
                explanation: analysisResult.Explanation,
                confidenceScore: analysisResult.ConfidenceScore,
                suggestionScope: "SOS_REQUEST",
                metadata: JsonSerializer.Serialize(new { rawResponse = aiResponse, analysisResult })
            );

            await _sosAiAnalysisRepository.CreateAsync(analysis, cancellationToken);

            // Update SOS request with AI suggested priority if not already set
            var sosRequest = await _sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
            if (sosRequest != null && string.IsNullOrEmpty(sosRequest.PriorityLevel))
            {
                sosRequest.PriorityLevel = analysisResult.Priority;
                await _sosRequestRepository.UpdateAsync(sosRequest, cancellationToken);
            }

            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "AI analysis completed for SOS Request Id={sosRequestId}: Priority={priority}, Severity={severity}, Confidence={confidence}",
                sosRequestId, analysisResult.Priority, analysisResult.SeverityLevel, analysisResult.ConfidenceScore);
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

    private async Task<string?> CallGeminiApiAsync(string model, string? systemPrompt, string userPrompt, double temperature, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = string.Format(GEMINI_API_URL, model, _apiKey);

            var requestBody = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Parts = new List<GeminiPart>
                        {
                            new() { Text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = temperature,
                    MaxOutputTokens = 2048
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error: {statusCode} - {error}", response.StatusCode, error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
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

    #region Gemini API Models

    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
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

    #endregion
}
