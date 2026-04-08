using System.Text;
using System.Text.Json;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Application.Common;

public static class SosPriorityRuleConfigSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> SevereMedicalIssues = new(StringComparer.OrdinalIgnoreCase)
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

    public static SosPriorityRuleConfigDocument DefaultConfig { get; } = new();

    public static SosPriorityRuleConfigDocument FromModel(SosPriorityRuleConfigModel? model)
    {
        if (!string.IsNullOrWhiteSpace(model?.ConfigJson))
        {
            return Deserialize(model.ConfigJson);
        }

        return model is null ? new SosPriorityRuleConfigDocument() : BuildLegacyCompatibleConfig(model);
    }

    public static SosPriorityRuleConfigDocument Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SosPriorityRuleConfigDocument();
        }

        try
        {
            return JsonSerializer.Deserialize<SosPriorityRuleConfigDocument>(json, JsonOptions)
                ?? new SosPriorityRuleConfigDocument();
        }
        catch
        {
            return new SosPriorityRuleConfigDocument();
        }
    }

    public static string Serialize(SosPriorityRuleConfigDocument config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public static void SyncLegacyFields(SosPriorityRuleConfigModel model, SosPriorityRuleConfigDocument config)
    {
        model.ConfigJson = Serialize(config);

        model.IssueWeightsJson = JsonSerializer.Serialize(
            config.MedicalScore.MedicalIssueSeverity.ToDictionary(
                pair => NormalizeKey(pair.Key).ToLowerInvariant(),
                pair => pair.Value));

        model.MedicalSevereIssuesJson = JsonSerializer.Serialize(
            SevereMedicalIssues.Select(issue => issue.ToLowerInvariant()).ToList());

        model.AgeWeightsJson = JsonSerializer.Serialize(
            config.MedicalScore.AgeWeights.ToDictionary(
                pair => NormalizeKey(pair.Key).ToLowerInvariant(),
                pair => pair.Value));

        model.RequestTypeScoresJson = config.PriorityScore.UseRequestTypeScore
            ? JsonSerializer.Serialize(new Dictionary<string, int>
            {
                ["rescue"] = 30,
                ["relief"] = 20,
                ["other"] = 10
            })
            : "{}";

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
        ValidateNonNegativeDictionary(config.SituationMultiplier, "situation_multiplier", errors, requirePositive: true);
        ValidateNonNegativeDictionary(config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore, "relief_score.supply_urgency_score.water_urgency_score", errors);
        ValidateNonNegativeDictionary(config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore, "relief_score.supply_urgency_score.food_urgency_score", errors);

        if (!config.SituationMultiplier.Keys.Any(key => string.Equals(NormalizeKey(key), "DEFAULT_WHEN_NULL", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("situation_multiplier phải có khóa DEFAULT_WHEN_NULL.");
        }

        ValidateUniqueOptions(config.UiOptions.WaterDuration, "ui_options.WATER_DURATION", errors);
        ValidateUniqueOptions(config.UiOptions.FoodDuration, "ui_options.FOOD_DURATION", errors);

        return errors;
    }

    public static bool IsSevereMedicalIssue(string? issue)
    {
        return SevereMedicalIssues.Contains(NormalizeKey(issue));
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

    private static SosPriorityRuleConfigDocument BuildLegacyCompatibleConfig(SosPriorityRuleConfigModel model)
    {
        var config = new SosPriorityRuleConfigDocument();

        try
        {
            var ageWeights = JsonSerializer.Deserialize<Dictionary<string, double>>(model.AgeWeightsJson, JsonOptions);
            if (ageWeights is { Count: > 0 })
            {
                config.MedicalScore.AgeWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in ageWeights)
                {
                    config.MedicalScore.AgeWeights[NormalizeKey(pair.Key)] = pair.Value;
                }
            }
        }
        catch { }

        try
        {
            var medicalIssueSeverity = JsonSerializer.Deserialize<Dictionary<string, double>>(model.IssueWeightsJson, JsonOptions);
            if (medicalIssueSeverity is { Count: > 0 })
            {
                config.MedicalScore.MedicalIssueSeverity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in medicalIssueSeverity)
                {
                    config.MedicalScore.MedicalIssueSeverity[NormalizeKey(pair.Key)] = pair.Value;
                }
            }
        }
        catch { }

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
        catch { }

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
        catch { }

        config.PriorityScore.UseRequestTypeScore = !string.IsNullOrWhiteSpace(model.RequestTypeScoresJson)
            && !string.Equals(model.RequestTypeScoresJson, "{}", StringComparison.Ordinal);

        return config;
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
}