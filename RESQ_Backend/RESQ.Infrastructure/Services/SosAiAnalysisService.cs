using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
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
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SosAiAnalysisService> _logger;

    public SosAiAnalysisService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        IAiConfigRepository aiConfigRepository,
        IPromptRepository promptRepository,
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

            var settings = _settingsResolver.Resolve(aiConfig);
            var userPrompt = BuildUserPrompt(
                prompt.UserPromptTemplate,
                effectiveSosRequest.StructuredData,
                effectiveSosRequest.RawMessage,
                effectiveSosRequest.SosType,
                task);

            var aiResponse = await CallAiApiAsync(settings, prompt.SystemPrompt, userPrompt, cancellationToken);
            if (aiResponse == null)
            {
                _logger.LogWarning("AI response is null for SOS Request Id={sosRequestId}", task.SosRequestId);
                return;
            }

            var analysisResult = ParseAiResponse(aiResponse, task);
            var metadata = BuildMetadata(aiResponse, analysisResult, prompt.Id, prompt.Version, settings.Provider.ToString(), task);

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

    private static string BuildUserPrompt(
        string? template,
        string? structuredData,
        string? rawMessage,
        string? sosType,
        SosAiAnalysisTask task)
    {
        var fallbackPrompt = $"""
            Analyze this SOS request and compare it with the current rule-based evaluation.
            SOS type: {sosType ?? "UNKNOWN"}
            Raw message: {rawMessage ?? "N/A"}
            Structured data: {structuredData ?? "N/A"}
            Current rule-based score: {task.RuleBasedScore:0.##}
            Current rule-based priority: {task.RuleBasedPriority ?? "Unknown"}
            Current rule version: {task.RuleVersion ?? "Unknown"}
            Rule breakdown: {task.RuleBreakdownJson ?? "N/A"}

            Return JSON with these fields:
            - suggested_priority
            - suggested_priority_score
            - suggested_severity_level
            - agrees_with_rule_base
            - explanation
            - needs_immediate_safe_transfer
            - can_wait_for_combined_mission
            - handling_reason

            The explanation must say why this score was chosen and whether AI agrees with the rule-based score. If AI disagrees, explain the gap.
            """;

        if (string.IsNullOrWhiteSpace(template))
            return fallbackPrompt;

        var prompt = template
            .Replace("{{structured_data}}", structuredData ?? "N/A")
            .Replace("{{raw_message}}", rawMessage ?? "N/A")
            .Replace("{{sos_type}}", sosType ?? "UNKNOWN")
            .Replace("{{rule_based_score}}", task.RuleBasedScore.ToString("0.##"))
            .Replace("{{rule_based_priority}}", task.RuleBasedPriority ?? "Unknown")
            .Replace("{{rule_version}}", task.RuleVersion ?? "Unknown")
            .Replace("{{rule_breakdown}}", task.RuleBreakdownJson ?? "N/A");

        if (!template.Contains("{{rule_based_score}}", StringComparison.OrdinalIgnoreCase))
        {
            prompt += $"""


                Current rule-based evaluation:
                - Score: {task.RuleBasedScore:0.##}
                - Priority: {task.RuleBasedPriority ?? "Unknown"}
                - Rule version: {task.RuleVersion ?? "Unknown"}
                - Breakdown: {task.RuleBreakdownJson ?? "N/A"}
                """;
        }

        if (!template.Contains("suggested_priority_score", StringComparison.OrdinalIgnoreCase))
        {
            prompt += """

                Return valid JSON with:
                - suggested_priority
                - suggested_priority_score
                - suggested_severity_level
                - agrees_with_rule_base
                - explanation
                - needs_immediate_safe_transfer
                - can_wait_for_combined_mission
                - handling_reason

                The explanation must include the reason for the numeric score and whether AI agrees with the current rule-based score.
                """;
        }

        return prompt;
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

    private static AiAnalysisResult ParseAiResponse(string response, SosAiAnalysisTask task)
    {
        try
        {
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
                    return NormalizeResult(parsed, task);
            }
        }
        catch
        {
        }

        var priority = ExtractPriority(response);
        var suggestedPriorityScore = InferPriorityScore(priority, task.RuleBasedScore, task.RuleBasedPriority);
        var agreesWithRuleBase = DetermineAgreement(
            suggestedPriorityScore,
            task.RuleBasedScore,
            priority,
            task.RuleBasedPriority);
        var explanation = EnsureExplanationMentionsRuleBase(
            response.Length > 500 ? response[..500] : response,
            suggestedPriorityScore,
            task.RuleBasedScore,
            agreesWithRuleBase,
            priority,
            task.RuleBasedPriority);

        return new AiAnalysisResult
        {
            Priority = priority,
            SeverityLevel = ExtractSeverity(response),
            SuggestedPriorityScore = suggestedPriorityScore,
            AgreesWithRuleBase = agreesWithRuleBase,
            Explanation = explanation,
            NeedsImmediateSafeTransfer = string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? true
                : null,
            CanWaitForCombinedMission = string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase)
                ? false
                : null,
            HandlingReason = explanation
        };
    }

    private static AiAnalysisResult NormalizeResult(AiAnalysisResult result, SosAiAnalysisTask task)
    {
        var normalizedPriority = NormalizePriority(result.SuggestedPriority ?? result.Priority) ?? "Medium";
        var normalizedSeverity = NormalizeSeverity(result.SuggestedSeverityLevel ?? result.SeverityLevel, normalizedPriority);
        var suggestedPriorityScore = result.SuggestedPriorityScore
            ?? InferPriorityScore(normalizedPriority, task.RuleBasedScore, task.RuleBasedPriority);
        var agreesWithRuleBase = result.AgreesWithRuleBase
            ?? DetermineAgreement(
                suggestedPriorityScore,
                task.RuleBasedScore,
                normalizedPriority,
                task.RuleBasedPriority);
        var explanation = EnsureExplanationMentionsRuleBase(
            result.Explanation,
            suggestedPriorityScore,
            task.RuleBasedScore,
            agreesWithRuleBase,
            normalizedPriority,
            task.RuleBasedPriority);
        var needsImmediateSafeTransfer = result.NeedsImmediateSafeTransfer
            ?? string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase);
        var canWaitForCombinedMission = result.CanWaitForCombinedMission
            ?? !string.Equals(normalizedPriority, "Critical", StringComparison.OrdinalIgnoreCase);

        return new AiAnalysisResult
        {
            Priority = normalizedPriority,
            SeverityLevel = normalizedSeverity,
            SuggestedPriority = normalizedPriority,
            SuggestedSeverityLevel = normalizedSeverity,
            SuggestedPriorityScore = suggestedPriorityScore,
            AgreesWithRuleBase = agreesWithRuleBase,
            Explanation = explanation,
            NeedsImmediateSafeTransfer = needsImmediateSafeTransfer,
            CanWaitForCombinedMission = canWaitForCombinedMission,
            HandlingReason = string.IsNullOrWhiteSpace(result.HandlingReason)
                ? explanation
                : result.HandlingReason
        };
    }

    private static string BuildMetadata(
        string aiResponse,
        AiAnalysisResult analysisResult,
        int promptId,
        string? promptVersion,
        string provider,
        SosAiAnalysisTask task)
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
            ruleBaseContext = new
            {
                score = task.RuleBasedScore,
                priority = task.RuleBasedPriority,
                ruleVersion = task.RuleVersion,
                breakdownJson = task.RuleBreakdownJson
            }
        });
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

    private static double InferPriorityScore(string? priority, double ruleBasedScore, string? ruleBasedPriority)
    {
        if (string.Equals(NormalizePriority(priority), NormalizePriority(ruleBasedPriority), StringComparison.OrdinalIgnoreCase))
            return ruleBasedScore;

        return NormalizePriority(priority) switch
        {
            "Critical" => 9.0,
            "High" => 7.0,
            "Medium" => 5.0,
            "Low" => 2.0,
            _ => ruleBasedScore
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

        return Math.Abs(suggestedPriorityScore - ruleBasedScore) <= 1.0d;
    }

    private static string EnsureExplanationMentionsRuleBase(
        string? explanation,
        double suggestedPriorityScore,
        double ruleBasedScore,
        bool? agreesWithRuleBase,
        string? suggestedPriority,
        string? ruleBasedPriority)
    {
        var baseExplanation = string.IsNullOrWhiteSpace(explanation)
            ? $"AI suggested score {suggestedPriorityScore:0.##} with priority {suggestedPriority ?? "Unknown"}."
            : explanation.Trim();

        var hasScoreMention = baseExplanation.Contains("score", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("diem", StringComparison.OrdinalIgnoreCase);
        var hasAgreementMention = baseExplanation.Contains("agree", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("dong y", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("khong dong y", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("rule-base", StringComparison.OrdinalIgnoreCase)
            || baseExplanation.Contains("rule base", StringComparison.OrdinalIgnoreCase);

        if (hasScoreMention && hasAgreementMention)
            return baseExplanation;

        var agreementText = agreesWithRuleBase == true
            ? $"AI agrees with the current rule-base score {ruleBasedScore:0.##} ({ruleBasedPriority ?? "Unknown"})."
            : $"AI does not agree with the current rule-base score {ruleBasedScore:0.##} ({ruleBasedPriority ?? "Unknown"}) and sees a different urgency level.";

        return $"{baseExplanation} AI suggested score {suggestedPriorityScore:0.##}. {agreementText}".Trim();
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
    }
}
