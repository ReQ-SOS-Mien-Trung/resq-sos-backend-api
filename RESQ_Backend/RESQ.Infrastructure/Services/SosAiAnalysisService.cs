using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
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
    private const double MinPriorityScore = 0d;
    private const double MaxPriorityScore = 100d;
    private const double DefaultAdjustmentLimit = 15d;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly IAiConfigRepository _aiConfigRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly ISosPriorityRuleConfigRepository _ruleConfigRepository;
    private readonly ISosAiAnalysisRepository _sosAiAnalysisRepository;
    private readonly ISosRequestRepository _sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SosAiAnalysisService> _logger;

    public SosAiAnalysisService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        IAiConfigRepository aiConfigRepository,
        IPromptRepository promptRepository,
        ISosPriorityRuleConfigRepository ruleConfigRepository,
        ISosAiAnalysisRepository sosAiAnalysisRepository,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        IUnitOfWork unitOfWork,
        ILogger<SosAiAnalysisService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _aiConfigRepository = aiConfigRepository;
        _promptRepository = promptRepository;
        _ruleConfigRepository = ruleConfigRepository;
        _sosAiAnalysisRepository = sosAiAnalysisRepository;
        _sosRequestRepository = sosRequestRepository;
        _sosRequestUpdateRepository = sosRequestUpdateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task AnalyzeAndSaveAsync(SosAiAnalysisTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting AI analysis for SOS Request Id={sosRequestId} with fingerprint={fingerprint}",
                task.SosRequestId,
                task.ContentFingerprint);

            var sosRequest = await _sosRequestRepository.GetByIdAsync(task.SosRequestId, cancellationToken);
            if (sosRequest is null)
            {
                _logger.LogWarning("SOS request not found for AI analysis. SosRequestId={sosRequestId}", task.SosRequestId);
                return;
            }

            var victimUpdateLookup = await _sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
                [task.SosRequestId],
                cancellationToken);
            victimUpdateLookup.TryGetValue(task.SosRequestId, out var latestVictimUpdate);
            var effectiveSosRequest = SosRequestVictimUpdateOverlay.Apply(sosRequest, latestVictimUpdate);
            var currentFingerprint = SosAiAnalysisTask.BuildContentFingerprint(
                effectiveSosRequest.StructuredData,
                effectiveSosRequest.RawMessage,
                effectiveSosRequest.SosType);

            if (!string.Equals(task.ContentFingerprint, currentFingerprint, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Skipping stale SOS AI analysis task for SosRequestId={sosRequestId}. QueuedFingerprint={queuedFingerprint}, CurrentFingerprint={currentFingerprint}",
                    task.SosRequestId,
                    task.ContentFingerprint,
                    currentFingerprint);
                return;
            }

            var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.SosPriorityAnalysis, cancellationToken);
            if (prompt == null)
            {
                _logger.LogWarning("No active prompt found for SosPriorityAnalysis. Skipping AI analysis.");
                return;
            }

            var aiConfig = await _aiConfigRepository.GetActiveAsync(cancellationToken);
            if (aiConfig == null)
            {
                _logger.LogWarning("No active AI config found. Skipping AI analysis for SOS Request Id={sosRequestId}.", task.SosRequestId);
                return;
            }

            var ruleConfigContext = await ResolveRuleConfigContextAsync(task, cancellationToken);
            var settings = _settingsResolver.Resolve(aiConfig);
            var userPrompt = BuildUserPrompt(
                prompt.UserPromptTemplate,
                effectiveSosRequest.StructuredData,
                effectiveSosRequest.RawMessage,
                effectiveSosRequest.SosType,
                task,
                ruleConfigContext);

            var aiResponse = await CallAiApiAsync(settings, prompt.SystemPrompt, userPrompt, cancellationToken);
            if (aiResponse == null)
            {
                _logger.LogWarning("AI response is null for SOS Request Id={sosRequestId}", task.SosRequestId);
                return;
            }

            var analysisResult = ParseAiResponse(aiResponse, task, ruleConfigContext.Config);
            var metadata = BuildMetadata(
                aiResponse,
                analysisResult,
                prompt.Id,
                prompt.Version,
                settings.Provider.ToString(),
                task,
                ruleConfigContext);

            var analysis = RESQ.Domain.Entities.Emergency.SosAiAnalysisModel.Create(
                sosRequestId: task.SosRequestId,
                modelName: settings.Model,
                modelVersion: prompt.Version,
                analysisType: "PRIORITY_ANALYSIS",
                suggestedSeverityLevel: analysisResult.SeverityLevel,
                suggestedPriority: analysisResult.Priority,
                suggestedPriorityScore: analysisResult.SuggestedPriorityScore ?? task.RuleBasedScore,
                agreesWithRuleBase: analysisResult.AgreesWithRuleBase,
                explanation: analysisResult.Explanation,
                suggestionScope: "SOS_REQUEST",
                metadata: metadata);

            await _sosAiAnalysisRepository.CreateAsync(analysis, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "AI analysis completed for SOS Request Id={sosRequestId}: Provider={provider}, Model={model}, Priority={priority}, Score={score}, Severity={severity}, AgreesWithRuleBase={agreesWithRuleBase}",
                task.SosRequestId,
                settings.Provider,
                settings.Model,
                analysisResult.Priority,
                analysisResult.SuggestedPriorityScore,
                analysisResult.SeverityLevel,
                analysisResult.AgreesWithRuleBase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI analysis for SOS Request Id={sosRequestId}", task.SosRequestId);
        }
    }

    private async Task<RuleConfigContext> ResolveRuleConfigContextAsync(
        SosAiAnalysisTask task,
        CancellationToken cancellationToken)
    {
        SosPriorityRuleConfigModel? model = null;
        var source = "default";

        if (task.RuleConfigId.HasValue)
        {
            model = await _ruleConfigRepository.GetByIdAsync(task.RuleConfigId.Value, cancellationToken);
            if (model is not null)
            {
                source = "task_config_id";
            }
        }

        if (model is null)
        {
            model = await _ruleConfigRepository.GetAsync(cancellationToken);
            source = model is null ? "built_in_default" : "active_config_fallback";
        }

        var config = SosPriorityRuleConfigSupport.FromModel(model);
        return new RuleConfigContext(model, config, source);
    }

    private static string BuildUserPrompt(
        string? template,
        string? structuredData,
        string? rawMessage,
        string? sosType,
        SosAiAnalysisTask task,
        RuleConfigContext ruleConfigContext)
    {
        var fallbackPrompt = $"""
            Phân tích yêu cầu SOS này như một lớp điều chỉnh trên kết quả chấm điểm rule-based hiện tại.
            Không chấm điểm lại từ đầu. Hãy dùng rule_config, rule_based_evaluation và sos_payload được cung cấp.
            """;

        var prompt = string.IsNullOrWhiteSpace(template)
            ? fallbackPrompt
            : template
                .Replace("{{structured_data}}", structuredData ?? "N/A")
                .Replace("{{raw_message}}", rawMessage ?? "N/A")
                .Replace("{{sos_type}}", sosType ?? "UNKNOWN")
                .Replace("{{rule_based_score}}", task.RuleBasedScore.ToString("0.##"))
                .Replace("{{rule_based_priority}}", task.RuleBasedPriority ?? "Unknown")
                .Replace("{{rule_version}}", task.RuleVersion ?? "Unknown")
                .Replace("{{rule_config_version}}", task.RuleConfigVersion ?? ruleConfigContext.Config.ConfigVersion)
                .Replace("{{rule_breakdown}}", task.RuleBreakdownJson ?? "N/A");

        var ruleConfigJson = SosPriorityRuleConfigSupport.Serialize(ruleConfigContext.Config);
        var ruleBasedEvaluationJson = JsonSerializer.Serialize(new
        {
            score_scale = "0-100",
            score = ClampScore(task.RuleBasedScore),
            priority = task.RuleBasedPriority ?? "Unknown",
            rule_version = task.RuleVersion ?? "Unknown",
            config_id = task.RuleConfigId,
            config_version = task.RuleConfigVersion ?? ruleConfigContext.Config.ConfigVersion,
            breakdown = ParseJsonForPrompt(task.RuleBreakdownJson)
        }, JsonOptions);
        var sosPayloadJson = JsonSerializer.Serialize(new
        {
            sos_type = sosType ?? "UNKNOWN",
            raw_message = rawMessage,
            structured_data = ParseJsonForPrompt(structuredData)
        }, JsonOptions);

        return prompt + $$"""


            NGÔN NGỮ BẮT BUỘC:
            - Chỉ giữ nguyên tên JSON property và enum/code value như Critical, High, Medium, Low, Severe, Moderate, Minor.
            - Mọi nội dung mô tả tự do trong explanation, handling_reason, guardrail_override_reason, uncovered_factors và rule_config_basis phải viết bằng tiếng Việt.
            - Không viết lẫn câu tiếng Anh trong các field mô tả.

            IMPORTANT SCORING CONTRACT:
            - The rule-based score is the baseline and already uses the rule_config below.
            - The score scale is 0-100. `suggested_priority_score` must be the final adjusted score on the 0-100 scale.
            - Do not score from scratch. Only adjust for concrete user-provided conditions that the rule_config or rule breakdown does not cover, under-weights, or over-weights.
            - If there is no extra factor, return the same score as rule_based_evaluation.score, `score_adjustment_delta = 0`, `adjustment_direction = "none"`, and `agrees_with_rule_base = true`.
            - Default adjustment guardrail is +/-15 points from the rule-based score.
            - Larger adjustment is allowed only when there is clear immediate life-threatening evidence and `guardrail_override_reason` explains it.
            - `suggested_priority` must match the final score and rule_config thresholds.

            Return valid JSON only with:
            {
              "suggested_priority": "Critical|High|Medium|Low",
              "suggested_priority_score": 0.0-100.0,
              "suggested_severity_level": "Critical|Severe|Moderate|Minor",
              "agrees_with_rule_base": true,
              "score_adjustment_delta": 0.0,
              "adjustment_direction": "increase|decrease|none",
              "uncovered_factors": [],
              "rule_config_basis": [],
              "additional_severe_flag": false,
              "guardrail_override_reason": null,
              "needs_immediate_safe_transfer": false,
              "can_wait_for_combined_mission": true,
              "handling_reason": "Giải thích bằng tiếng Việt vì sao SOS này có thể hoặc không thể chờ ghép mission.",
              "explanation": "Giải thích bằng tiếng Việt cách rule_config và yếu tố bổ sung dẫn tới điểm cuối cùng."
            }

            rule_config:
            {{ruleConfigJson}}

            rule_based_evaluation:
            {{ruleBasedEvaluationJson}}

            sos_payload:
            {{sosPayloadJson}}
            """;
    }

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
            _logger.LogError(
                ex,
                "Error calling AI API (Provider={provider}, Model={model}, URL={url})",
                settings.Provider,
                settings.Model,
                settings.ApiUrl);
            return null;
        }
    }

    private static AiAnalysisResult ParseAiResponse(
        string response,
        SosAiAnalysisTask task,
        SosPriorityRuleConfigDocument ruleConfig)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AiAnalysisResult>(jsonStr, JsonOptions);

                if (parsed != null)
                    return NormalizeResult(parsed, task, ruleConfig);
            }
        }
        catch
        {
        }

        var ruleBasedScore = ClampScore(task.RuleBasedScore);
        var ruleBasedSevere = ResolveRuleBasedSevereFlag(task);
        var priority = DeterminePriorityFromScore(ruleBasedScore, ruleBasedSevere, ruleConfig);
        var agreesWithRuleBase = DetermineAgreement(ruleBasedScore, task.RuleBasedScore, priority, task.RuleBasedPriority);
        var explanation = EnsureExplanationMentionsRuleBase(
            null,
            ruleBasedScore,
            task.RuleBasedScore,
            agreesWithRuleBase,
            priority,
            task.RuleBasedPriority);

        return new AiAnalysisResult
        {
            Priority = priority,
            SeverityLevel = ExtractSeverity(response),
            SuggestedPriority = priority,
            SuggestedPriorityScore = ruleBasedScore,
            AgreesWithRuleBase = agreesWithRuleBase,
            Explanation = explanation,
            NeedsImmediateSafeTransfer = string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? true
                : null,
            CanWaitForCombinedMission = string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? false
                : null,
            HandlingReason = explanation,
            ScoreAdjustmentDelta = 0d,
            AdjustmentDirection = "none",
            AdditionalSevereFlag = false,
            GuardrailApplied = false,
            GuardrailLimit = DefaultAdjustmentLimit
        };
    }

    private static AiAnalysisResult NormalizeResult(
        AiAnalysisResult result,
        SosAiAnalysisTask task,
        SosPriorityRuleConfigDocument ruleConfig)
    {
        var requestedPriorityScore = result.SuggestedPriorityScore;
        var originalRequestedScore = requestedPriorityScore.HasValue
            ? ClampScore(requestedPriorityScore.Value)
            : (double?)null;
        var ruleBasedSevere = ResolveRuleBasedSevereFlag(task);
        var additionalSevereFlag = result.AdditionalSevereFlag == true;
        var guardrail = ApplyScoreGuardrail(
            originalRequestedScore ?? task.RuleBasedScore,
            result,
            task,
            ruleBasedSevere);
        var finalScore = guardrail.Score;
        var hasSevereFlag = ruleBasedSevere || additionalSevereFlag;
        var normalizedPriority = DeterminePriorityFromScore(finalScore, hasSevereFlag, ruleConfig);
        var normalizedSeverity = NormalizeSeverity(result.SuggestedSeverityLevel ?? result.SeverityLevel, normalizedPriority);
        var scoreDelta = Math.Round(finalScore - ClampScore(task.RuleBasedScore), 2);
        var agreesWithRuleBase = DetermineAgreement(
            finalScore,
            task.RuleBasedScore,
            normalizedPriority,
            task.RuleBasedPriority);
        var explanation = EnsureExplanationMentionsRuleBase(
            result.Explanation,
            finalScore,
            task.RuleBasedScore,
            agreesWithRuleBase,
            normalizedPriority,
            task.RuleBasedPriority);
        var needsImmediateSafeTransfer = result.NeedsImmediateSafeTransfer
            ?? string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase);
        var canWaitForCombinedMission = result.CanWaitForCombinedMission
            ?? !string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase);
        var handlingReason = NormalizeVietnameseNarrative(result.HandlingReason);

        return new AiAnalysisResult
        {
            Priority = normalizedPriority,
            SeverityLevel = normalizedSeverity,
            SuggestedPriority = normalizedPriority,
            SuggestedSeverityLevel = normalizedSeverity,
            SuggestedPriorityScore = finalScore,
            AgreesWithRuleBase = agreesWithRuleBase,
            Explanation = explanation,
            NeedsImmediateSafeTransfer = needsImmediateSafeTransfer,
            CanWaitForCombinedMission = canWaitForCombinedMission,
            HandlingReason = string.IsNullOrWhiteSpace(handlingReason)
                ? explanation
                : handlingReason,
            ScoreAdjustmentDelta = scoreDelta,
            AdjustmentDirection = ResolveAdjustmentDirection(scoreDelta),
            UncoveredFactors = result.UncoveredFactors ?? [],
            RuleConfigBasis = result.RuleConfigBasis ?? [],
            AdditionalSevereFlag = additionalSevereFlag,
            GuardrailOverrideReason = NormalizeVietnameseNarrative(result.GuardrailOverrideReason),
            GuardrailApplied = guardrail.Applied,
            GuardrailLimit = DefaultAdjustmentLimit,
            OriginalSuggestedPriorityScore = originalRequestedScore
        };
    }

    private static string BuildMetadata(
        string aiResponse,
        AiAnalysisResult analysisResult,
        int promptId,
        string? promptVersion,
        string provider,
        SosAiAnalysisTask task,
        RuleConfigContext ruleConfigContext)
    {
        return JsonSerializer.Serialize(new
        {
            rawResponse = aiResponse,
            analysisResult,
            promptId,
            promptVersion,
            provider,
            contentFingerprint = task.ContentFingerprint,
            queuedAtUtc = task.QueuedAtUtc,
            adjustmentContract = new
            {
                scoreScale = "0-100",
                defaultAdjustmentLimit = DefaultAdjustmentLimit,
                ruleBasedBaselineRequired = true
            },
            ruleBaseContext = new
            {
                score = task.RuleBasedScore,
                priority = task.RuleBasedPriority,
                ruleVersion = task.RuleVersion,
                configId = task.RuleConfigId,
                configVersion = task.RuleConfigVersion,
                breakdownJson = task.RuleBreakdownJson
            },
            ruleConfigContext = new
            {
                source = ruleConfigContext.Source,
                configId = ruleConfigContext.Model?.Id ?? task.RuleConfigId,
                configVersion = ruleConfigContext.Config.ConfigVersion,
                config = ruleConfigContext.Config
            }
        }, JsonOptions);
    }

    private static string ExtractPriority(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("CRITICAL"))
            return "Critical";
        if (upper.Contains("HIGH"))
            return "High";
        if (upper.Contains("MEDIUM"))
            return "Medium";
        return "Low";
    }

    private static string ExtractSeverity(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("LIFE-THREATENING") || upper.Contains("CRITICAL"))
            return "Critical";
        if (upper.Contains("SEVERE") || upper.Contains("HIGH"))
            return "Severe";
        if (upper.Contains("MODERATE") || upper.Contains("MEDIUM"))
            return "Moderate";
        return "Minor";
    }

    private static string? NormalizePriority(string? priority)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(priority) switch
        {
            "CRITICAL" => "Critical",
            "HIGH" => "High",
            "MEDIUM" or "MODERATE" => "Medium",
            "LOW" or "MINOR" => "Low",
            _ => string.IsNullOrWhiteSpace(priority) ? null : priority.Trim()
        };
    }

    private static string NormalizeSeverity(string? severity, string? priority)
    {
        var normalizedSeverity = SosPriorityRuleConfigSupport.NormalizeKey(severity) switch
        {
            "CRITICAL" or "LIFE_THREATENING" => "Critical",
            "SEVERE" or "HIGH" => "Severe",
            "MODERATE" or "MEDIUM" => "Moderate",
            "MINOR" or "LOW" => "Minor",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(normalizedSeverity))
            return normalizedSeverity;

        return NormalizePriority(priority) switch
        {
            "Critical" => "Critical",
            "High" => "Severe",
            "Medium" => "Moderate",
            _ => "Minor"
        };
    }

    private static bool DetermineAgreement(
        double suggestedPriorityScore,
        double ruleBasedScore,
        string? suggestedPriority,
        string? ruleBasedPriority)
    {
        var samePriority = string.Equals(
            NormalizePriority(suggestedPriority),
            NormalizePriority(ruleBasedPriority),
            StringComparison.OrdinalIgnoreCase);

        if (!samePriority)
            return false;

        return Math.Abs(ClampScore(suggestedPriorityScore) - ClampScore(ruleBasedScore)) <= 0.5d;
    }

    private static double ClampScore(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return MinPriorityScore;
        }

        return Math.Clamp(score, MinPriorityScore, MaxPriorityScore);
    }

    private static string DeterminePriorityFromScore(
        double score,
        bool hasSevereFlag,
        SosPriorityRuleConfigDocument ruleConfig)
    {
        return SosPriorityRuleConfigSupport
            .DeterminePriorityLevel(ClampScore(score), hasSevereFlag, ruleConfig)
            .ToString();
    }

    private static bool ResolveRuleBasedSevereFlag(SosAiAnalysisTask task)
    {
        try
        {
            var details = JsonSerializer.Deserialize<SosPriorityEvaluationDetails>(
                task.RuleBreakdownJson ?? string.Empty,
                JsonOptions);
            if (details is not null)
            {
                return details.HasSevereFlag
                    || details.ThresholdDecision?.HasSevereFlag == true
                    || details.MedicalSevereFlag
                    || details.SituationSevereFlag;
            }
        }
        catch
        {
        }

        return Enum.TryParse<SosPriorityLevel>(task.RuleBasedPriority, ignoreCase: true, out var priority)
            && priority is SosPriorityLevel.Critical or SosPriorityLevel.High;
    }

    private static GuardrailResult ApplyScoreGuardrail(
        double requestedScore,
        AiAnalysisResult result,
        SosAiAnalysisTask task,
        bool ruleBasedSevere)
    {
        var baseline = ClampScore(task.RuleBasedScore);
        var requested = ClampScore(requestedScore);
        var delta = requested - baseline;

        if (Math.Abs(delta) <= DefaultAdjustmentLimit)
        {
            return new GuardrailResult(requested, false);
        }

        if (IsGuardrailOverrideAllowed(result, task, ruleBasedSevere))
        {
            return new GuardrailResult(requested, false);
        }

        var cappedScore = ClampScore(baseline + (Math.Sign(delta) * DefaultAdjustmentLimit));
        return new GuardrailResult(cappedScore, true);
    }

    private static bool IsGuardrailOverrideAllowed(
        AiAnalysisResult result,
        SosAiAnalysisTask task,
        bool ruleBasedSevere)
    {
        if (string.IsNullOrWhiteSpace(result.GuardrailOverrideReason))
        {
            return false;
        }

        return ruleBasedSevere || HasImmediateLifeThreateningEvidence(task);
    }

    private static bool HasImmediateLifeThreateningEvidence(SosAiAnalysisTask task)
    {
        var text = $"{task.RawMessage} {task.StructuredData}".ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] markers =
        [
            "UNCONSCIOUS",
            "BREATHING_DIFFICULTY",
            "CHEST_PAIN_STROKE",
            "DROWNING",
            "SEVERELY_BLEEDING",
            "LIFE_THREATENING",
            "CANNOT_MOVE",
            "CAN_NOT_MOVE",
            "TRAPPED",
            "COLLAPSED",
            "FLOODING",
            "DANGER_ZONE",
            "SEVERE",
            "CRITICAL"
        ];

        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveAdjustmentDirection(double scoreDelta)
    {
        if (scoreDelta > 0.5d)
            return "increase";
        if (scoreDelta < -0.5d)
            return "decrease";
        return "none";
    }

    private static object? ParseJsonForPrompt(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return json.Trim();
        }
    }

    private static string EnsureExplanationMentionsRuleBase(
        string? explanation,
        double suggestedPriorityScore,
        double ruleBasedScore,
        bool? agreesWithRuleBase,
        string? suggestedPriority,
        string? ruleBasedPriority)
    {
        var baseExplanation = NormalizeVietnameseNarrative(explanation)
            ?? $"AI đề xuất điểm ưu tiên {suggestedPriorityScore:0.##} dựa trên dữ liệu SOS và điểm theo luật hiện tại.";

        var hasScoreMention = baseExplanation.Contains("score", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("diem", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("điểm", StringComparison.OrdinalIgnoreCase);
        var hasAgreementMention = baseExplanation.Contains("agree", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("dong y", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("đồng ý", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("khong dong y", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("không đồng ý", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("rule-base", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("rule base", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("luật", StringComparison.OrdinalIgnoreCase);

        if (hasScoreMention && hasAgreementMention)
            return baseExplanation;

        var agreementText = agreesWithRuleBase == true
            ? $"AI đồng ý với điểm theo luật hiện tại {ruleBasedScore:0.##} ({ruleBasedPriority ?? "Unknown"})."
            : $"AI không đồng ý với điểm theo luật hiện tại {ruleBasedScore:0.##} ({ruleBasedPriority ?? "Unknown"}) vì mức độ khẩn cấp thực tế khác.";

        var parts = new List<string> { baseExplanation };
        if (!hasScoreMention)
            parts.Add($"Điểm AI đề xuất là {suggestedPriorityScore:0.##}.");
        if (!hasAgreementMention)
            parts.Add(agreementText);

        return string.Join(" ", parts).Trim();
    }

    private static string? NormalizeVietnameseNarrative(string? text)
    {
        var sanitized = AiTextSanitizer.RemoveBackendEnglishSuffix(text);
        if (string.IsNullOrWhiteSpace(sanitized))
            return null;

        return LooksLikeEnglishNarrative(sanitized)
            ? null
            : sanitized;
    }

    private static bool LooksLikeEnglishNarrative(string text)
    {
        if (HasVietnameseDiacritic(text))
            return false;

        var lower = text.ToLowerInvariant();
        string[] englishMarkers =
        [
            "rule config",
            "rule-base",
            "rule base",
            "current rule",
            "score",
            "priority",
            "life-threatening",
            "structured data",
            "supports",
            "adjustment",
            "extra factor",
            "general concern",
            "urgent",
            "severe"
        ];

        return englishMarkers.Any(marker => lower.Contains(marker));
    }

    private static bool HasVietnameseDiacritic(string text)
    {
        const string vietnameseCharacters =
            "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ" +
            "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ";

        return text.Any(c => vietnameseCharacters.Contains(c));
    }

    private sealed class AiAnalysisResult
    {
        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "Medium";

        [JsonPropertyName("suggested_priority")]
        public string? SuggestedPriority { get; set; }

        [JsonPropertyName("severity_level")]
        public string SeverityLevel { get; set; } = "Moderate";

        [JsonPropertyName("suggested_severity_level")]
        public string? SuggestedSeverityLevel { get; set; }

        [JsonPropertyName("suggested_priority_score")]
        public double? SuggestedPriorityScore { get; set; }

        [JsonPropertyName("agrees_with_rule_base")]
        public bool? AgreesWithRuleBase { get; set; }

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        [JsonPropertyName("needs_immediate_safe_transfer")]
        public bool? NeedsImmediateSafeTransfer { get; set; }

        [JsonPropertyName("can_wait_for_combined_mission")]
        public bool? CanWaitForCombinedMission { get; set; }

        [JsonPropertyName("handling_reason")]
        public string? HandlingReason { get; set; }

        [JsonPropertyName("score_adjustment_delta")]
        public double? ScoreAdjustmentDelta { get; set; }

        [JsonPropertyName("adjustment_direction")]
        public string? AdjustmentDirection { get; set; }

        [JsonPropertyName("uncovered_factors")]
        public List<string>? UncoveredFactors { get; set; }

        [JsonPropertyName("rule_config_basis")]
        public List<string>? RuleConfigBasis { get; set; }

        [JsonPropertyName("additional_severe_flag")]
        public bool? AdditionalSevereFlag { get; set; }

        [JsonPropertyName("guardrail_override_reason")]
        public string? GuardrailOverrideReason { get; set; }

        [JsonPropertyName("guardrail_applied")]
        public bool? GuardrailApplied { get; set; }

        [JsonPropertyName("guardrail_limit")]
        public double? GuardrailLimit { get; set; }

        [JsonPropertyName("original_suggested_priority_score")]
        public double? OriginalSuggestedPriorityScore { get; set; }
    }

    private sealed record RuleConfigContext(
        SosPriorityRuleConfigModel? Model,
        SosPriorityRuleConfigDocument Config,
        string Source);

    private readonly record struct GuardrailResult(double Score, bool Applied);
}
