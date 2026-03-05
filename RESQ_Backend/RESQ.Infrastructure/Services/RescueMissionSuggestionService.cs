using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services;

public class RescueMissionSuggestionService : IRescueMissionSuggestionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPromptRepository _promptRepository;
    private readonly ILogger<RescueMissionSuggestionService> _logger;
    private readonly string _apiKey;

    // Fallback defaults - chỉ dùng khi field trong DB bị null
    private const string FALLBACK_MODEL = "gemini-2.5-flash";
    private const string FALLBACK_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const double FALLBACK_TEMPERATURE = 0.5;
    private const int FALLBACK_MAX_TOKENS = 65535;

    public RescueMissionSuggestionService(
        IHttpClientFactory httpClientFactory,
        IPromptRepository promptRepository,
        IConfiguration configuration,
        ILogger<RescueMissionSuggestionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _promptRepository = promptRepository;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        bool isMultiDepotRecommended = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Generating rescue mission suggestion for {count} SOS requests", sosRequests.Count);

            // Load prompt config from database theo PromptType
            var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.MissionPlanning, cancellationToken);
            if (prompt == null)
            {
                _logger.LogWarning("Không tìm thấy prompt đang active cho loại MissionPlanning trong database.");
                return new RescueMissionSuggestionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Chưa có prompt 'MissionPlanning' đang được kích hoạt. Vui lòng cấu hình trong quản trị hệ thống.",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            var modelName = prompt.Model ?? FALLBACK_MODEL;
            var apiUrl = prompt.ApiUrl ?? FALLBACK_API_URL;
            var temperature = prompt.Temperature ?? FALLBACK_TEMPERATURE;
            // Always use at least FALLBACK_MAX_TOKENS regardless of DB setting to avoid truncation
            // Vietnamese text is token-heavy (~2-3 tokens/char), so responses need much more than 4096
            var maxTokens = Math.Max(prompt.MaxTokens ?? FALLBACK_MAX_TOKENS, FALLBACK_MAX_TOKENS);

            _logger.LogInformation(
                "Using AI config from DB: Model={model}, Temperature={temperature}, MaxTokens={maxTokens}",
                modelName, temperature, maxTokens);

            // Build data for prompt
            var sosDataJson = BuildSosRequestsData(sosRequests);
            var depotsDataJson = BuildDepotsData(nearbyDepots);

            var userPrompt = (prompt.UserPromptTemplate ?? string.Empty)
                .Replace("{{sos_requests_data}}", sosDataJson)
                .Replace("{{total_count}}", sosRequests.Count.ToString())
                .Replace("{{depots_data}}", depotsDataJson);

            // Nếu prompt template không có placeholder {{depots_data}} nhưng có kho → đính kèm cuối prompt
            if (!string.IsNullOrEmpty(depotsDataJson)
                && !(prompt.UserPromptTemplate ?? string.Empty).Contains("{{depots_data}}"))
            {
                userPrompt += $"""


--- THÔNG TIN KHO TIẾP TẾ PHÙ HỢP ---
Dưới đây là danh sách các kho tiếp tế đang hoạt động, còn hàng, được sắp xếp ưu tiên theo mức độ đáp ứng vật tư nhu cầu SOS rồi đến gần nhất.
Mỗi kho có danh sách vật tư khả dụng (quantity - reserved). Hãy ưu tiên kho đáp ứng đúng vật tư cần thiết để đề xuất nguồn cung cấp tài nguyên:
{depotsDataJson}
""";
            }

            // Nếu không kho nào đủ đồ trong một lần, thông báo AI phối hợp nhiều kho
            if (isMultiDepotRecommended)
            {
                userPrompt += """


--- LƯU Ý NGƯỚN THÜ CẤP PHÁT ---
Phân tích tồn kho cho thấy không có kho đơn lẻ nào có đủ tất cả các loại vật tư cần thiết.
TRONG KẾC HOẠCH BÁO CẠO: viết rõ kho nào cung cấp loại gì để điều phối viên biết cần lấy từ nhiều nguồn.
""";
            }

            // Call Gemini API
            var aiResponse = await CallAiApiAsync(modelName, apiUrl, prompt.SystemPrompt ?? string.Empty, userPrompt, temperature, maxTokens, cancellationToken);

            stopwatch.Stop();

            if (aiResponse == null)
            {
                return new RescueMissionSuggestionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "AI không phản hồi. Vui lòng thử lại sau.",
                    ModelName = modelName,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Parse AI response
            var result = ParseMissionSuggestion(aiResponse);
            result.IsSuccess = true;
            result.ModelName = modelName;
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.RawAiResponse = aiResponse;

            _logger.LogInformation(
                "Rescue mission suggestion generated: Title={title}, Type={type}, Priority={priority}, Activities={activityCount}, Confidence={confidence}",
                result.SuggestedMissionTitle, result.SuggestedMissionType, result.SuggestedPriorityScore,
                result.SuggestedActivities.Count, result.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating rescue mission suggestion");
            return new RescueMissionSuggestionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Lỗi khi gọi AI: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static string BuildSosRequestsData(List<SosRequestSummary> sosRequests)
    {
        var entries = sosRequests.Select((sos, index) => new
        {
            stt = index + 1,
            id = sos.Id,
            loai_sos = sos.SosType ?? "Không xác định",
            tin_nhan = sos.RawMessage,
            du_lieu_chi_tiet = sos.StructuredData ?? "Không có",
            muc_uu_tien = sos.PriorityLevel ?? "Chưa đánh giá",
            trang_thai = sos.Status ?? "Không rõ",
            vi_tri = sos.Latitude.HasValue && sos.Longitude.HasValue
                ? $"{sos.Latitude}, {sos.Longitude}"
                : "Không xác định",
            thoi_gian_cho_phut = sos.WaitTimeMinutes,
            thoi_gian_tao = sos.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"
        });

        return JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>
    /// Tuần tự hoá danh sách kho tiếp tế gần nhất sang JSON để đưa vào prompt cho AI.
    /// Trả về chuỗi rỗng nếu không có kho nào.
    /// </summary>
    private static string BuildDepotsData(List<DepotSummary>? depots)
    {
        if (depots is null || depots.Count == 0)
            return string.Empty;

        var entries = depots.Select((d, index) => new
        {
            stt = index + 1,
            id = d.Id,
            ten_kho = d.Name,
            dia_chi = d.Address,
            vi_tri = d.Latitude.HasValue && d.Longitude.HasValue
                ? $"{d.Latitude}, {d.Longitude}"
                : "Không xác định",
            khoang_cach_km = d.DistanceKm,
            suc_chua_tong = d.Capacity,
            dang_su_dung = d.CurrentUtilization,
            con_trong = d.Capacity - d.CurrentUtilization,
            trang_thai = d.Status,
            vat_tu_kha_dung = d.Inventories.Count > 0
                ? d.Inventories.Select(i => new
                  {
                      ten = i.ItemName,
                      don_vi = i.Unit ?? "cái",
                      so_luong_kha_dung = i.AvailableQuantity
                  }).ToList()
                : null
        });

        return JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private async Task<string?> CallAiApiAsync(string model, string apiUrlTemplate, string systemPrompt, string userPrompt, double temperature, int maxTokens, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);

        var url = string.Format(apiUrlTemplate, model, _apiKey);

        var requestBody = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Parts = [new() { Text = $"{systemPrompt}\n\n{userPrompt}" }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = temperature,
                MaxOutputTokens = maxTokens
            }
        };

        var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("AI API error: {statusCode} - {error}", response.StatusCode, error);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
    }

    private static RescueMissionSuggestionResult ParseMissionSuggestion(string response)
    {
        // Step 1: Strip ```json ... ``` markdown fence if present
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```"))
        {
            var fenceEnd = cleaned.IndexOf('\n');
            if (fenceEnd >= 0)
                cleaned = cleaned[(fenceEnd + 1)..];
            var closingFence = cleaned.LastIndexOf("```");
            if (closingFence >= 0)
                cleaned = cleaned[..closingFence];
            cleaned = cleaned.Trim();
        }

        // Step 2: Extract JSON object boundaries
        var jsonStart = cleaned.IndexOf('{');
        var jsonEnd = cleaned.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = cleaned[jsonStart..(jsonEnd + 1)];

            // Step 3: Try full deserialization
            try
            {
                var parsed = JsonSerializer.Deserialize<AiMissionSuggestion>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
                if (parsed != null)
                    return MapParsedToResult(parsed);
            }
            catch { /* fall through to partial extraction */ }

            // Step 4: Partial extraction via JsonDocument (handles valid-but-incomplete structures)
            try
            {
                return ExtractPartialFromJson(jsonStr);
            }
            catch { /* fall through to regex */ }
        }

        // Step 5: Regex extraction for severely truncated responses
        return ExtractViaRegex(cleaned.Length > 0 ? cleaned : response);
    }

    private static RescueMissionSuggestionResult MapParsedToResult(AiMissionSuggestion parsed)
    {
        return new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = parsed.MissionTitle,
            SuggestedMissionType = parsed.MissionType,
            SuggestedPriorityScore = parsed.PriorityScore > 0 ? parsed.PriorityScore : null,
            SuggestedSeverityLevel = parsed.SeverityLevel,
            OverallAssessment = parsed.OverallAssessment,
            SuggestedActivities = parsed.Activities?.Select(a => new SuggestedActivityDto
            {
                Step = a.Step,
                ActivityType = a.ActivityType ?? string.Empty,
                Description = a.Description ?? string.Empty,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                SuppliesToCollect = a.SuppliesToCollect?.Select(s => new SupplyToCollectDto
                {
                    ItemName = s.ItemName ?? string.Empty,
                    Quantity = s.Quantity,
                    Unit = s.Unit
                }).ToList()
            }).ToList() ?? [],
            SuggestedResources = parsed.Resources?.Select(r => new SuggestedResourceDto
            {
                ResourceType = r.ResourceType ?? string.Empty,
                Description = r.Description ?? string.Empty,
                Quantity = r.Quantity,
                Priority = r.Priority
            }).ToList() ?? [],
            EstimatedDuration = parsed.EstimatedDuration,
            SpecialNotes = parsed.SpecialNotes,
            ConfidenceScore = parsed.ConfidenceScore
        };
    }

    private static RescueMissionSuggestionResult ExtractPartialFromJson(string jsonStr)
    {
        using var doc = JsonDocument.Parse(jsonStr, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var root = doc.RootElement;
        var result = new RescueMissionSuggestionResult();

        if (root.TryGetProperty("mission_title", out var t)) result.SuggestedMissionTitle = t.GetString();
        if (root.TryGetProperty("mission_type", out var mt)) result.SuggestedMissionType = mt.GetString();
        if (root.TryGetProperty("priority_score", out var ps) && ps.TryGetDouble(out var psVal)) result.SuggestedPriorityScore = psVal;
        if (root.TryGetProperty("severity_level", out var sl)) result.SuggestedSeverityLevel = sl.GetString();
        if (root.TryGetProperty("overall_assessment", out var oa)) result.OverallAssessment = oa.GetString();
        if (root.TryGetProperty("estimated_duration", out var ed)) result.EstimatedDuration = ed.GetString();
        if (root.TryGetProperty("special_notes", out var sn)) result.SpecialNotes = sn.GetString();
        if (root.TryGetProperty("confidence_score", out var cs) && cs.TryGetDouble(out var csVal)) result.ConfidenceScore = csVal;

        if (root.TryGetProperty("activities", out var acts) && acts.ValueKind == JsonValueKind.Array)
        {
            result.SuggestedActivities = acts.EnumerateArray().Select(a =>
            {
                var dto = new SuggestedActivityDto();
                if (a.TryGetProperty("step", out var sv) && sv.TryGetInt32(out var svi)) dto.Step = svi;
                if (a.TryGetProperty("activity_type", out var at)) dto.ActivityType = at.GetString() ?? string.Empty;
                if (a.TryGetProperty("description", out var d)) dto.Description = d.GetString() ?? string.Empty;
                if (a.TryGetProperty("priority", out var p)) dto.Priority = p.GetString();
                if (a.TryGetProperty("estimated_time", out var et)) dto.EstimatedTime = et.GetString();
                if (a.TryGetProperty("depot_id", out var di) && di.ValueKind != JsonValueKind.Null && di.TryGetInt32(out var div)) dto.DepotId = div;
                if (a.TryGetProperty("depot_name", out var dn) && dn.ValueKind != JsonValueKind.Null) dto.DepotName = dn.GetString();
                if (a.TryGetProperty("depot_address", out var da) && da.ValueKind != JsonValueKind.Null) dto.DepotAddress = da.GetString();
                if (a.TryGetProperty("supplies_to_collect", out var stc) && stc.ValueKind == JsonValueKind.Array)
                    dto.SuppliesToCollect = stc.EnumerateArray().Select(s =>
                    {
                        var supply = new SupplyToCollectDto();
                        if (s.TryGetProperty("item_name", out var iname)) supply.ItemName = iname.GetString() ?? string.Empty;
                        if (s.TryGetProperty("quantity", out var qty) && qty.TryGetInt32(out var qtyv)) supply.Quantity = qtyv;
                        if (s.TryGetProperty("unit", out var unit) && unit.ValueKind != JsonValueKind.Null) supply.Unit = unit.GetString();
                        return supply;
                    }).ToList();
                return dto;
            }).ToList();
        }

        if (root.TryGetProperty("resources", out var ress) && ress.ValueKind == JsonValueKind.Array)
        {
            result.SuggestedResources = ress.EnumerateArray().Select(r =>
            {
                var dto = new SuggestedResourceDto();
                if (r.TryGetProperty("resource_type", out var rt)) dto.ResourceType = rt.GetString() ?? string.Empty;
                if (r.TryGetProperty("description", out var d)) dto.Description = d.GetString() ?? string.Empty;
                if (r.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qv)) dto.Quantity = qv;
                if (r.TryGetProperty("priority", out var p)) dto.Priority = p.GetString();
                return dto;
            }).ToList();
        }

        return result;
    }

    private static RescueMissionSuggestionResult ExtractViaRegex(string text)
    {
        static string? ExtractStr(string src, string field)
        {
            var m = Regex.Match(src, $@"""{field}""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Singleline);
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }
        static double? ExtractNum(string src, string field)
        {
            var m = Regex.Match(src, $@"""{field}""\s*:\s*([0-9]+(?:\.[0-9]+)?)");
            return m.Success && double.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        return new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = ExtractStr(text, "mission_title") ?? "Nhiệm vụ giải cứu",
            SuggestedMissionType = ExtractStr(text, "mission_type"),
            SuggestedPriorityScore = ExtractNum(text, "priority_score"),
            SuggestedSeverityLevel = ExtractStr(text, "severity_level"),
            OverallAssessment = ExtractStr(text, "overall_assessment"),
            EstimatedDuration = ExtractStr(text, "estimated_duration"),
            SpecialNotes = ExtractStr(text, "special_notes"),
            ConfidenceScore = ExtractNum(text, "confidence_score") ?? 0.3
        };
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

    #endregion

    #region AI Response Models

    private class AiMissionSuggestion
    {
        [JsonPropertyName("mission_title")]
        public string? MissionTitle { get; set; }

        [JsonPropertyName("mission_type")]
        public string? MissionType { get; set; }

        [JsonPropertyName("priority_score")]
        public double PriorityScore { get; set; }

        [JsonPropertyName("severity_level")]
        public string? SeverityLevel { get; set; }

        [JsonPropertyName("overall_assessment")]
        public string? OverallAssessment { get; set; }

        [JsonPropertyName("activities")]
        public List<AiActivity>? Activities { get; set; }

        [JsonPropertyName("resources")]
        public List<AiResource>? Resources { get; set; }

        [JsonPropertyName("estimated_duration")]
        public string? EstimatedDuration { get; set; }

        [JsonPropertyName("special_notes")]
        public string? SpecialNotes { get; set; }

        [JsonPropertyName("confidence_score")]
        public double ConfidenceScore { get; set; }
    }

    private class AiSupplyToCollect
    {
        [JsonPropertyName("item_name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    private class AiActivity
    {
        [JsonPropertyName("step")]
        public int Step { get; set; }

        [JsonPropertyName("activity_type")]
        public string? ActivityType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("estimated_time")]
        public string? EstimatedTime { get; set; }

        [JsonPropertyName("depot_id")]
        public int? DepotId { get; set; }

        [JsonPropertyName("depot_name")]
        public string? DepotName { get; set; }

        [JsonPropertyName("depot_address")]
        public string? DepotAddress { get; set; }

        [JsonPropertyName("supplies_to_collect")]
        public List<AiSupplyToCollect>? SuppliesToCollect { get; set; }
    }

    private class AiResource
    {
        [JsonPropertyName("resource_type")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }
    }

    #endregion
}
