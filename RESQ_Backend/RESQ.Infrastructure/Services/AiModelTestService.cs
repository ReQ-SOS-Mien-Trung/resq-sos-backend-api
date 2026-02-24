using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services;

public class AiModelTestService : IAiModelTestService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiModelTestService> _logger;
    private readonly string _apiKey;

    private const string TEST_PROMPT = "Respond with exactly: {\"status\": \"ok\", \"message\": \"Model is working\"}";

    public AiModelTestService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiModelTestService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task<AiModelTestResult> TestModelAsync(
        string model,
        string apiUrlTemplate,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var url = string.Format(apiUrlTemplate, model, _apiKey);

            var requestBody = new TestGeminiRequest
            {
                Contents = new List<TestGeminiContent>
                {
                    new()
                    {
                        Parts = new List<TestGeminiPart>
                        {
                            new() { Text = TEST_PROMPT }
                        }
                    }
                },
                GenerationConfig = new TestGeminiGenerationConfig
                {
                    Temperature = temperature,
                    MaxOutputTokens = maxTokens
                }
            };

            _logger.LogInformation("Testing AI model: {model}, URL template: {url}", model, apiUrlTemplate);

            var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI model test failed: {statusCode} - {error}", response.StatusCode, errorBody);

                return new AiModelTestResult
                {
                    IsSuccess = false,
                    Model = model,
                    ErrorMessage = $"API trả về lỗi: {(int)response.StatusCode} {response.ReasonPhrase}. Chi tiết: {Truncate(errorBody, 500)}",
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            var result = await response.Content.ReadFromJsonAsync<TestGeminiResponse>(cancellationToken: cancellationToken);
            var aiText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            _logger.LogInformation("AI model test succeeded: {model}, ResponseTime={ms}ms", model, stopwatch.ElapsedMilliseconds);

            return new AiModelTestResult
            {
                IsSuccess = true,
                Model = model,
                AiResponse = Truncate(aiText, 500),
                HttpStatusCode = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            return new AiModelTestResult
            {
                IsSuccess = false,
                Model = model,
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
                Model = model,
                ErrorMessage = $"Không thể kết nối đến AI API: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error testing AI model {model}", model);
            return new AiModelTestResult
            {
                IsSuccess = false,
                Model = model,
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

    #region Gemini API Models (for test)

    private class TestGeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<TestGeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public TestGeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class TestGeminiContent
    {
        [JsonPropertyName("parts")]
        public List<TestGeminiPart> Parts { get; set; } = [];
    }

    private class TestGeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class TestGeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private class TestGeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<TestGeminiCandidate>? Candidates { get; set; }
    }

    private class TestGeminiCandidate
    {
        [JsonPropertyName("content")]
        public TestGeminiContent? Content { get; set; }
    }

    #endregion
}
