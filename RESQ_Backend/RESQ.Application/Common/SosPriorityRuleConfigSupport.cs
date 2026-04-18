using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Common;

public static class SosPriorityRuleConfigSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> DefaultSevereMedicalIssues = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNCONSCIOUS",
        "BREATHING_DIFFICULTY",
        "CHEST_PAIN_STROKE",
        "DROWNING",
        "SEVERELY_BLEEDING"
    };

    private static readonly HashSet<string> SevereSituations = new(StringComparer.OrdinalIgnoreCase)
    {
        "FLOODING",
        "COLLAPSED"
    };

    private static readonly HashSet<string> VulnerabilityExpressionVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "VULNERABILITY_RAW",
        "SUPPLY_URGENCY_SCORE",
        "CAP_RATIO"
    };

    private static readonly HashSet<string> ReliefExpressionVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUPPLY_URGENCY_SCORE",
        "VULNERABILITY_SCORE"
    };

    private static readonly HashSet<string> PriorityExpressionVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "MEDICAL_SCORE",
        "RELIEF_SCORE",
        "SITUATION_MULTIPLIER",
        "REQUEST_TYPE_SCORE"
    };

    public static SosPriorityRuleConfigDocument DefaultConfig { get; } = new();

    public static SosPriorityRuleConfigDocument FromModel(SosPriorityRuleConfigModel? model)
    {
        var config = !string.IsNullOrWhiteSpace(model?.ConfigJson)
            ? Deserialize(model.ConfigJson)
            : model is null
                ? new SosPriorityRuleConfigDocument()
                : BuildLegacyCompatibleConfig(model);

        if (model is not null)
        {
            if (!string.IsNullOrWhiteSpace(model.ConfigVersion))
            {
                config.ConfigVersion = model.ConfigVersion;
            }

            config.IsActive = model.IsActive;
        }

        return EnsureDefaults(config);
    }

    public static SosPriorityRuleConfigDocument Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SosPriorityRuleConfigDocument();
        }

        try
        {
            return EnsureDefaults(
                JsonSerializer.Deserialize<SosPriorityRuleConfigDocument>(json, JsonOptions)
                ?? new SosPriorityRuleConfigDocument());
        }
        catch
        {
            return new SosPriorityRuleConfigDocument();
        }
    }

    public static string Serialize(SosPriorityRuleConfigDocument config)
    {
        return JsonSerializer.Serialize(EnsureDefaults(config), JsonOptions);
    }

    public static void SyncLegacyFields(SosPriorityRuleConfigModel model, SosPriorityRuleConfigDocument config)
    {
        config = EnsureDefaults(config);
        model.ConfigVersion = string.IsNullOrWhiteSpace(config.ConfigVersion)
            ? "SOS_PRIORITY_V2"
            : config.ConfigVersion;
        model.IsActive = config.IsActive;
        model.ConfigJson = Serialize(config);

        model.IssueWeightsJson = JsonSerializer.Serialize(
            config.MedicalScore.MedicalIssueSeverity.ToDictionary(
                pair => NormalizeKey(pair.Key).ToLowerInvariant(),
                pair => pair.Value));

        model.MedicalSevereIssuesJson = JsonSerializer.Serialize(
            config.MedicalSevereIssues
                .Select(NormalizeKey)
                .Where(issue => !string.IsNullOrWhiteSpace(issue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(issue => issue.ToLowerInvariant())
                .ToList());

        model.AgeWeightsJson = JsonSerializer.Serialize(
            config.MedicalScore.AgeWeights.ToDictionary(
                pair => NormalizeKey(pair.Key).ToLowerInvariant(),
                pair => pair.Value));

        model.RequestTypeScoresJson = JsonSerializer.Serialize(
            config.RequestTypeScores.ToDictionary(
                pair => NormalizeKey(pair.Key).ToLowerInvariant(),
                pair => pair.Value));

        model.SituationMultipliersJson = JsonSerializer.Serialize(
            config.SituationMultiplier
                .Where(pair => !string.Equals(NormalizeKey(pair.Key), "DEFAULT_WHEN_NULL", StringComparison.OrdinalIgnoreCase))
                .Select(pair => new
                {
                    keys = new[] { NormalizeKey(pair.Key).ToLowerInvariant() },
                    multiplier = pair.Value,
                    severe = IsSevereSituation(pair.Key)
                })
                .ToList());

        model.PriorityThresholdsJson = JsonSerializer.Serialize(new
        {
            critical = new { minScore = (double)config.PriorityLevel.P1Threshold, requireSevere = true },
            high = new { minScore = (double)config.PriorityLevel.P2Threshold, requireSevere = true },
            medium = new { minScore = (double)config.PriorityLevel.P3Threshold, requireSevere = false }
        });

        model.WaterUrgencyScoresJson = JsonSerializer.Serialize(config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore);
        model.FoodUrgencyScoresJson = JsonSerializer.Serialize(config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore);
        model.BlanketUrgencyRulesJson = JsonSerializer.Serialize(config.ReliefScore.SupplyUrgencyScore.BlanketUrgencyScore);
        model.ClothingUrgencyRulesJson = JsonSerializer.Serialize(config.ReliefScore.SupplyUrgencyScore.ClothingUrgencyScore);
        model.VulnerabilityRulesJson = JsonSerializer.Serialize(new
        {
            vulnerability_raw = config.ReliefScore.VulnerabilityScore.VulnerabilityRaw,
            cap_ratio = config.ReliefScore.VulnerabilityScore.CapRatio
        });
        model.VulnerabilityScoreExpressionJson = JsonSerializer.Serialize(config.ReliefScore.VulnerabilityScore.Expression, JsonOptions);
        model.ReliefScoreExpressionJson = JsonSerializer.Serialize(config.ReliefScore.Expression, JsonOptions);
        model.PriorityScoreExpressionJson = JsonSerializer.Serialize(config.PriorityScore.Expression, JsonOptions);
    }

    public static SosPriorityRuleConfigDocument EnsureDefaults(SosPriorityRuleConfigDocument? config)
    {
        config ??= new SosPriorityRuleConfigDocument();
        config.DisplayLabels ??= new SosDisplayLabelsConfig();

        MergeLabelMap(config.DisplayLabels.MedicalIssues, DefaultConfig.DisplayLabels.MedicalIssues);
        MergeLabelMap(config.DisplayLabels.Situations, DefaultConfig.DisplayLabels.Situations);
        MergeLabelMap(config.DisplayLabels.WaterDuration, DefaultConfig.DisplayLabels.WaterDuration);
        MergeLabelMap(config.DisplayLabels.FoodDuration, DefaultConfig.DisplayLabels.FoodDuration);
        MergeLabelMap(config.DisplayLabels.AgeGroups, DefaultConfig.DisplayLabels.AgeGroups);
        MergeLabelMap(config.DisplayLabels.RequestTypes, DefaultConfig.DisplayLabels.RequestTypes);

        return config;
    }

    public static SosPriorityLevel DeterminePriorityLevel(
        double totalScore,
        bool hasSevereFlag,
        SosPriorityRuleConfigDocument config)
    {
        if (totalScore >= config.PriorityLevel.P1Threshold && hasSevereFlag)
        {
            return SosPriorityLevel.Critical;
        }

        if (totalScore >= config.PriorityLevel.P2Threshold && hasSevereFlag)
        {
            return SosPriorityLevel.High;
        }

        if (totalScore >= config.PriorityLevel.P3Threshold)
        {
            return SosPriorityLevel.Medium;
        }

        return SosPriorityLevel.Low;
    }

    public static double GetMinimumScoreForPriority(SosPriorityLevel priorityLevel, SosPriorityRuleConfigDocument config)
    {
        return priorityLevel switch
        {
            SosPriorityLevel.Critical => config.PriorityLevel.P1Threshold,
            SosPriorityLevel.High => config.PriorityLevel.P2Threshold,
            SosPriorityLevel.Medium => config.PriorityLevel.P3Threshold,
            _ => 0d
        };
    }

    public static IReadOnlyList<string> GetValidationErrors(SosPriorityRuleConfigDocument? config)
    {
        var errors = new List<string>();
        if (config is null)
        {
            errors.Add("Config không được để trống.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(config.ConfigVersion))
        {
            errors.Add("config_version là bắt buộc.");
        }

        if (config.PriorityLevel.P1Threshold <= config.PriorityLevel.P2Threshold
            || config.PriorityLevel.P2Threshold <= config.PriorityLevel.P3Threshold
            || config.PriorityLevel.P3Threshold < 0)
        {
            errors.Add("Ngưỡng ưu tiên phải thỏa P1 > P2 > P3 >= 0.");
        }

        if (config.ReliefScore.VulnerabilityScore.CapRatio is < 0 or > 1)
        {
            errors.Add("relief_score.vulnerability_score.cap_ratio phải nằm trong khoảng từ 0 đến 1.");
        }

        if (config.UiConstraints.MinTotalPeopleToProceed < 1)
        {
            errors.Add("ui_constraints.MIN_TOTAL_PEOPLE_TO_PROCEED phải lớn hơn hoặc bằng 1.");
        }

        if (config.UiConstraints.BlanketRequestCountMin < 1)
        {
            errors.Add("ui_constraints.BLANKET_REQUEST_COUNT_MIN phải lớn hơn hoặc bằng 1.");
        }

        if (config.UiConstraints.BlanketRequestCountDefault < config.UiConstraints.BlanketRequestCountMin)
        {
            errors.Add("ui_constraints.BLANKET_REQUEST_COUNT_DEFAULT không được nhỏ hơn BLANKET_REQUEST_COUNT_MIN.");
        }

        ValidateNonNegativeDictionary(config.MedicalScore.AgeWeights, "medical_score.age_weights", errors);
        ValidateNonNegativeDictionary(config.MedicalScore.MedicalIssueSeverity, "medical_score.medical_issue_severity", errors);
        ValidateNonNegativeDictionary(config.RequestTypeScores, "request_type_scores", errors);
        ValidateNonNegativeDictionary(config.SituationMultiplier, "situation_multiplier", errors, requirePositive: true);
        ValidateNonNegativeDictionary(config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore, "relief_score.supply_urgency_score.water_urgency_score", errors);
        ValidateNonNegativeDictionary(config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore, "relief_score.supply_urgency_score.food_urgency_score", errors);

        if (!config.RequestTypeScores.Keys.Any(key => string.Equals(NormalizeKey(key), "OTHER", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("request_type_scores phải có khóa OTHER.");
        }

        if (!config.SituationMultiplier.Keys.Any(key => string.Equals(NormalizeKey(key), "DEFAULT_WHEN_NULL", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("situation_multiplier phải có khóa DEFAULT_WHEN_NULL.");
        }

        ValidateUniqueOptions(config.UiOptions.WaterDuration, "ui_options.WATER_DURATION", errors);
        ValidateUniqueOptions(config.UiOptions.FoodDuration, "ui_options.FOOD_DURATION", errors);
        ValidateUniqueOptions(config.MedicalSevereIssues, "medical_severe_issues", errors);

        errors.AddRange(SosExpressionEngine.Validate(
            config.ReliefScore.VulnerabilityScore.Expression,
            "relief_score.vulnerability_score.expression",
            VulnerabilityExpressionVariables));

        errors.AddRange(SosExpressionEngine.Validate(
            config.ReliefScore.Expression,
            "relief_score.expression",
            ReliefExpressionVariables));

        errors.AddRange(SosExpressionEngine.Validate(
            config.PriorityScore.Expression,
            "priority_score.expression",
            PriorityExpressionVariables));

        var priorityVariables = SosExpressionEngine.CollectNormalizedVariables(config.PriorityScore.Expression);
        if (!config.PriorityScore.UseRequestTypeScore && priorityVariables.Contains("REQUEST_TYPE_SCORE"))
        {
            errors.Add("priority_score.expression không được dùng request_type_score khi priority_score.use_request_type_score = false.");
        }

        return errors;
    }

    public static bool IsSevereMedicalIssue(string? issue, SosPriorityRuleConfigDocument? config = null)
    {
        var normalizedIssue = NormalizeKey(issue);
        if (string.IsNullOrWhiteSpace(normalizedIssue))
        {
            return false;
        }

        if (config?.MedicalSevereIssues is { Count: > 0 })
        {
            return config.MedicalSevereIssues
                .Select(NormalizeKey)
                .Contains(normalizedIssue, StringComparer.OrdinalIgnoreCase);
        }

        return DefaultSevereMedicalIssues.Contains(normalizedIssue);
    }

    public static bool IsSevereSituation(string? situation)
    {
        return SevereSituations.Contains(NormalizeKey(situation));
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length * 2);
        char? previousAppended = null;

        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];

            if (char.IsLetterOrDigit(character))
            {
                if (char.IsUpper(character)
                    && builder.Length > 0
                    && previousAppended.HasValue
                    && previousAppended != '_'
                    && char.IsLower(previousAppended.Value))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToUpperInvariant(character));
                previousAppended = builder[^1];
                continue;
            }

            if (builder.Length > 0 && previousAppended != '_')
            {
                builder.Append('_');
                previousAppended = '_';
            }
        }

        return builder.ToString().Trim('_');
    }

    private static void MergeLabelMap(IDictionary<string, string>? target, IReadOnlyDictionary<string, string> defaults)
    {
        if (target is null)
        {
            return;
        }

        foreach (var entry in defaults)
        {
            if (target.ContainsKey(entry.Key))
            {
                continue;
            }

            target[entry.Key] = entry.Value;
        }
    }

    private static SosPriorityRuleConfigDocument BuildLegacyCompatibleConfig(SosPriorityRuleConfigModel model)
    {
        var config = new SosPriorityRuleConfigDocument
        {
            ConfigVersion = string.IsNullOrWhiteSpace(model.ConfigVersion) ? "SOS_PRIORITY_V2" : model.ConfigVersion,
            IsActive = model.IsActive
        };

        TryApplyDictionary(model.AgeWeightsJson, config.MedicalScore.AgeWeights);
        TryApplyDictionary(model.IssueWeightsJson, config.MedicalScore.MedicalIssueSeverity);
        TryApplyDictionary(model.RequestTypeScoresJson, config.RequestTypeScores);
        TryApplyDictionary(model.WaterUrgencyScoresJson, config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore);
        TryApplyDictionary(model.FoodUrgencyScoresJson, config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore);

        TryApplyList(model.MedicalSevereIssuesJson, config.MedicalSevereIssues);

        TryApplyObject(
            model.BlanketUrgencyRulesJson,
            config.ReliefScore.SupplyUrgencyScore.BlanketUrgencyScore,
            value => config.ReliefScore.SupplyUrgencyScore.BlanketUrgencyScore = value);
        TryApplyObject(
            model.ClothingUrgencyRulesJson,
            config.ReliefScore.SupplyUrgencyScore.ClothingUrgencyScore,
            value => config.ReliefScore.SupplyUrgencyScore.ClothingUrgencyScore = value);
        TryApplyObject(model.VulnerabilityScoreExpressionJson, config.ReliefScore.VulnerabilityScore.Expression, value => config.ReliefScore.VulnerabilityScore.Expression = value);
        TryApplyObject(model.ReliefScoreExpressionJson, config.ReliefScore.Expression, value => config.ReliefScore.Expression = value);
        TryApplyObject(model.PriorityScoreExpressionJson, config.PriorityScore.Expression, value => config.PriorityScore.Expression = value);

        try
        {
            var vulnerabilityRules = JsonSerializer.Deserialize<LegacyVulnerabilityRules>(model.VulnerabilityRulesJson, JsonOptions);
            if (vulnerabilityRules is not null)
            {
                if (vulnerabilityRules.VulnerabilityRaw is not null)
                {
                    config.ReliefScore.VulnerabilityScore.VulnerabilityRaw = vulnerabilityRules.VulnerabilityRaw;
                }

                if (vulnerabilityRules.CapRatio.HasValue)
                {
                    config.ReliefScore.VulnerabilityScore.CapRatio = vulnerabilityRules.CapRatio.Value;
                }
            }
        }
        catch
        {
        }

        try
        {
            var legacyThresholds = JsonSerializer.Deserialize<LegacyPriorityThresholds>(model.PriorityThresholdsJson, JsonOptions);
            if (legacyThresholds is not null)
            {
                config.PriorityLevel.P1Threshold = (int)Math.Round(legacyThresholds.Critical?.MinScore ?? config.PriorityLevel.P1Threshold);
                config.PriorityLevel.P2Threshold = (int)Math.Round(legacyThresholds.High?.MinScore ?? config.PriorityLevel.P2Threshold);
                config.PriorityLevel.P3Threshold = (int)Math.Round(legacyThresholds.Medium?.MinScore ?? config.PriorityLevel.P3Threshold);
            }
        }
        catch
        {
        }

        try
        {
            var legacySituations = JsonSerializer.Deserialize<List<LegacySituationRule>>(model.SituationMultipliersJson, JsonOptions);
            if (legacySituations is { Count: > 0 })
            {
                config.SituationMultiplier = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var rule in legacySituations.Where(rule => rule.Keys is { Count: > 0 }))
                {
                    foreach (var key in rule.Keys)
                    {
                        config.SituationMultiplier[NormalizeKey(key)] = rule.Multiplier;
                    }
                }

                if (!config.SituationMultiplier.ContainsKey("DEFAULT_WHEN_NULL"))
                {
                    config.SituationMultiplier["DEFAULT_WHEN_NULL"] = 1.0;
                }
            }
        }
        catch
        {
        }

        return config;
    }

    private static void TryApplyDictionary<TValue>(string? json, IDictionary<string, TValue> target)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, TValue>>(json ?? "{}", JsonOptions);
            if (parsed is not { Count: > 0 })
            {
                return;
            }

            target.Clear();
            foreach (var pair in parsed)
            {
                target[NormalizeKey(pair.Key)] = pair.Value;
            }
        }
        catch
        {
        }
    }

    private static void TryApplyList(string? json, List<string> target)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json ?? "[]", JsonOptions);
            if (parsed is not { Count: > 0 })
            {
                return;
            }

            target.Clear();
            foreach (var value in parsed.Select(NormalizeKey).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                target.Add(value);
            }
        }
        catch
        {
        }
    }

    private static void TryApplyObject<T>(string? json, T _, Action<T> setter)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json, "{}", StringComparison.Ordinal))
            {
                return;
            }

            var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (parsed is not null)
            {
                setter(parsed);
            }
        }
        catch
        {
        }
    }

    private static void ValidateNonNegativeDictionary<TValue>(
        IDictionary<string, TValue>? values,
        string fieldName,
        List<string> errors,
        bool requirePositive = false)
        where TValue : struct, IComparable<TValue>
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{fieldName} không được để trống.");
            return;
        }

        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                errors.Add($"{fieldName} không được chứa khóa rỗng.");
                continue;
            }

            if (pair.Value.CompareTo(default) < 0 || (requirePositive && pair.Value.CompareTo(default) == 0))
            {
                errors.Add($"{fieldName}.{pair.Key} phải {(requirePositive ? "lớn hơn 0" : "không âm")}.");
            }
        }
    }

    private static void ValidateUniqueOptions(List<string>? values, string fieldName, List<string> errors)
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{fieldName} không được để trống.");
            return;
        }

        var normalized = values.Select(NormalizeKey).ToList();
        if (normalized.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{fieldName} không được chứa giá trị rỗng.");
        }

        if (normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add($"{fieldName} không được chứa giá trị trùng lặp.");
        }
    }

    private sealed class LegacySituationRule
    {
        public List<string> Keys { get; set; } = [];
        public double Multiplier { get; set; }
    }

    private sealed class LegacyPriorityThresholds
    {
        public LegacyThresholdEntry? Critical { get; set; }
        public LegacyThresholdEntry? High { get; set; }
        public LegacyThresholdEntry? Medium { get; set; }
    }

    private sealed class LegacyThresholdEntry
    {
        public double MinScore { get; set; }
    }

    private sealed class LegacyVulnerabilityRules
    {
        [JsonPropertyName("vulnerability_raw")]
        public SosVulnerabilityRawConfig? VulnerabilityRaw { get; set; }

        [JsonPropertyName("cap_ratio")]
        public double? CapRatio { get; set; }
    }
}
