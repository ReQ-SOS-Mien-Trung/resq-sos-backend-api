using System.Text.Json;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Infrastructure.Services;

public class SosPriorityEvaluationService(ISosPriorityRuleConfigRepository ruleConfigRepository) : ISosPriorityEvaluationService
{
    private readonly ISosPriorityRuleConfigRepository _ruleConfigRepository = ruleConfigRepository;

    // ── Default fallback values (used when DB config is unavailable) ──

    private static readonly Dictionary<string, double> DefaultIssueWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unconscious"] = 5, ["drowning"] = 5,
        ["breathingDifficulty"] = 5, ["breathing_difficulty"] = 5,
        ["chestPainStroke"] = 5, ["chest_pain_stroke"] = 5,
        ["severelyBleeding"] = 4, ["severely_bleeding"] = 4,
        ["bleeding"] = 4, ["burns"] = 4,
        ["headInjury"] = 4, ["head_injury"] = 4,
        ["cannotMove"] = 4, ["cannot_move"] = 4,
        ["highFever"] = 3, ["high_fever"] = 3,
        ["dehydration"] = 3, ["fracture"] = 3,
        ["infantNeedsMilk"] = 3, ["infant_needs_milk"] = 3,
        ["lostParent"] = 3, ["lost_parent"] = 3,
        ["chronicDisease"] = 2, ["chronic_disease"] = 2,
        ["confusion"] = 2,
        ["needsMedicalDevice"] = 2, ["needs_medical_device"] = 2,
        ["other"] = 1,
    };

    private static readonly HashSet<string> DefaultMedicalSevereIssues = new(StringComparer.OrdinalIgnoreCase)
    {
        "unconscious", "drowning",
        "breathingDifficulty", "breathing_difficulty",
        "chestPainStroke", "chest_pain_stroke",
        "severelyBleeding", "severely_bleeding"
    };

    // ── JSON options ──
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<SosRuleEvaluationModel> EvaluateAsync(
        int sosRequestId,
        string? structuredDataJson,
        string? sosType,
        CancellationToken cancellationToken = default)
    {
        // Load config from DB; fall back to embedded defaults if none found
        var dbConfig = await _ruleConfigRepository.GetAsync(cancellationToken);

        var issueWeight = ParseIssueWeights(dbConfig?.IssueWeightsJson);
        var medicalSevereIssues = ParseMedicalSevereIssues(dbConfig?.MedicalSevereIssuesJson);
        var ageWeights = ParseAgeWeights(dbConfig?.AgeWeightsJson);
        var requestTypeScores = ParseRequestTypeScores(dbConfig?.RequestTypeScoresJson);
        var situationMultipliers = ParseSituationMultipliers(dbConfig?.SituationMultipliersJson);
        var priorityThresholds = ParsePriorityThresholds(dbConfig?.PriorityThresholdsJson);

        var evaluation = new SosRuleEvaluationModel
        {
            SosRequestId = sosRequestId,
            RuleVersion = "3.0",
            CreatedAt = DateTime.UtcNow
        };

        StructuredData? data = null;
        if (!string.IsNullOrWhiteSpace(structuredDataJson))
        {
            try { data = JsonSerializer.Deserialize<StructuredData>(structuredDataJson, _jsonOptions); }
            catch { }
        }

        // ── 1. requestTypeScore ──
        int requestTypeScore = GetRequestTypeScore(sosType, requestTypeScores);

        // ── 2. situationMultiplier + situationSevere flag ──
        // Dual-read: prefer new nested format, fallback to old flat
        var situation = data?.Incident?.Situation ?? data?.Situation;
        var (situationMultiplier, situationSevere) = GetSituationMultiplier(situation, situationMultipliers);

        // ── 3. medicalScore ──
        // Dual-read: build injured persons from new victims or old injured_persons
        double medicalScore = 0;
        bool medicalSevere = false;
        var injuredPersons = BuildInjuredPersons(data);
        if (injuredPersons is { Count: > 0 })
        {
            foreach (var person in injuredPersons)
            {
                var issues = person.MedicalIssues ?? [];
                double issueScore = issues.Sum(k => issueWeight.GetValueOrDefault(k, 1));
                double ageMultiplier = GetAgeWeight(person.PersonType, ageWeights);
                medicalScore += issueScore * ageMultiplier;
                if (!medicalSevere && issues.Any(k => medicalSevereIssues.Contains(k)))
                    medicalSevere = true;
            }
        }

        // ── 4. Assemble scores ──
        evaluation.EnvironmentScore = requestTypeScore;
        evaluation.MedicalScore = medicalScore;
        evaluation.InjuryScore = 0;
        evaluation.FoodScore = 0;
        evaluation.MobilityScore = 0;
        evaluation.TotalScore = (requestTypeScore + medicalScore) * situationMultiplier;

        // ── 5. Triage ──
        bool isSevere = medicalSevere || situationSevere;
        evaluation.PriorityLevel = DeterminePriorityLevel(evaluation.TotalScore, isSevere, priorityThresholds);
        evaluation.ItemsNeeded = DetermineItemsNeeded(data, sosType);

        return evaluation;
    }

    // ── Config parsers (return defaults on any parse failure) ──

    private static Dictionary<string, double> ParseIssueWeights(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DefaultIssueWeight;
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            if (raw is { Count: > 0 })
                return new Dictionary<string, double>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch { }
        return DefaultIssueWeight;
    }

    private static HashSet<string> ParseMedicalSevereIssues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DefaultMedicalSevereIssues;
        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json);
            if (raw is { Count: > 0 })
                return new HashSet<string>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch { }
        return DefaultMedicalSevereIssues;
    }

    private static Dictionary<string, double> ParseAgeWeights(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new(); }
        catch { return new(); }
    }

    private static Dictionary<string, int> ParseRequestTypeScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new(); }
        catch { return new(); }
    }

    private static List<SituationRule> ParseSituationMultipliers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<SituationRule>>(json) ?? []; }
        catch { return []; }
    }

    private static PriorityThresholdConfig ParsePriorityThresholds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<PriorityThresholdConfig>(json) ?? new(); }
        catch { return new(); }
    }

    // ── Scoring helpers ──

    private static double GetAgeWeight(string? personType, Dictionary<string, double> ageWeights)
    {
        if (ageWeights.Count > 0 && personType is not null && ageWeights.TryGetValue(personType, out var w))
            return w;
        // fallback
        return personType?.ToLowerInvariant() switch
        {
            "child" or "trẻ em" => 1.4,
            "elderly" or "elder" or "người già" => 1.3,
            _ => 1.0
        };
    }

    private static int GetRequestTypeScore(string? sosType, Dictionary<string, int> scores)
    {
        if (scores.Count > 0 && sosType is not null && scores.TryGetValue(sosType, out var s))
            return s;
        // fallback
        return sosType?.ToLowerInvariant() switch { "rescue" => 30, "relief" => 20, _ => 10 };
    }

    private static (double multiplier, bool severe) GetSituationMultiplier(string? situation, List<SituationRule> rules)
    {
        if (rules.Count > 0 && situation is not null)
        {
            var rule = rules.FirstOrDefault(r =>
                r.Keys.Any(k => k.Equals(situation, StringComparison.OrdinalIgnoreCase)));
            if (rule is not null)
                return (rule.Multiplier, rule.Severe);
        }
        // fallback
        return situation?.ToLowerInvariant() switch
        {
            "flooding" or "flood" => (1.5, true),
            "building_collapse" or "collapsed" or "collapse" => (1.5, true),
            "trapped" => (1.3, false),
            "danger_zone" or "dangerous_area" or "dangerous" => (1.3, false),
            "cannot_move" or "cannotmove" => (1.2, false),
            _ => (1.0, false)
        };
    }

    private static SosPriorityLevel DeterminePriorityLevel(double totalScore, bool isSevere, PriorityThresholdConfig thresholds)
    {
        double criticalMin = thresholds.Critical?.MinScore ?? 70;
        bool criticalNeedsSevere = thresholds.Critical?.RequireSevere ?? true;
        double highMin = thresholds.High?.MinScore ?? 45;
        bool highNeedsSevere = thresholds.High?.RequireSevere ?? true;
        double mediumMin = thresholds.Medium?.MinScore ?? 25;

        if (totalScore >= criticalMin && (!criticalNeedsSevere || isSevere)) return SosPriorityLevel.Critical;
        if (totalScore >= highMin && (!highNeedsSevere || isSevere)) return SosPriorityLevel.High;
        if (totalScore >= mediumMin) return SosPriorityLevel.Medium;
        return SosPriorityLevel.Low;
    }

    private static string? DetermineItemsNeeded(StructuredData? data, string? sosType)
    {
        var items = new List<string>();

        // Dual-read: build injured persons from new victims or old injured_persons
        var injuredPersons = BuildInjuredPersons(data);
        var hasInjured = data?.Incident?.HasInjured ?? data?.HasInjured;
        var peopleCount = data?.Incident?.PeopleCount ?? data?.PeopleCount;
        var situation = data?.Incident?.Situation ?? data?.Situation;

        var allIssues = injuredPersons?
            .SelectMany(p => p.MedicalIssues ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        if (injuredPersons?.Count > 0 || hasInjured == true)
        {
            items.Add("FIRST_AID_KIT");
            items.Add("MEDICAL_SUPPLIES");
        }

        if (allIssues.Contains("bleeding") || allIssues.Contains("severelyBleeding") || allIssues.Contains("severely_bleeding"))
        {
            items.Add("BANDAGES");
            items.Add("BLOOD_CLOTTING_AGENTS");
        }

        if (situation?.ToLowerInvariant() is "flooding")
        {
            items.Add("LIFE_JACKET");
            items.Add("RESCUE_BOAT");
            items.Add("ROPE");
        }

        if (situation?.ToLowerInvariant() is "trapped" or "building_collapse")
        {
            items.Add("ROPE");
            items.Add("RESCUE_EQUIPMENT");
        }

        if (situation?.ToLowerInvariant() is "fire")
        {
            items.Add("FIRE_EXTINGUISHER");
            items.Add("PROTECTIVE_GEAR");
        }

        if (peopleCount is { } pc)
        {
            var total = (pc.Adult ?? 0) + (pc.Child ?? 0) + (pc.Elderly ?? 0);
            if (total > 0)
            {
                items.Add("FOOD_RATIONS");
                items.Add("WATER");
                items.Add("BLANKETS");
            }
        }

        if (sosType?.ToLowerInvariant() is "evacuation")
        {
            items.Add("TRANSPORT_VEHICLE");
            items.Add("STRETCHER");
        }

        return items.Count > 0 ? JsonSerializer.Serialize(items) : null;
    }

    // ── Dual-read helper ──

    private static List<InjuredPerson>? BuildInjuredPersons(StructuredData? data)
    {
        if (data?.Victims is { Count: > 0 } victims)
        {
            return victims.Select(v => new InjuredPerson
            {
                PersonType = v.PersonType,
                MedicalIssues = v.IncidentStatus?.MedicalIssues,
                Severity = v.IncidentStatus?.Severity
            }).ToList();
        }
        return data?.InjuredPersons;
    }

    // ── Config model classes ──

    private class SituationRule
    {
        public List<string> Keys { get; set; } = [];
        public double Multiplier { get; set; } = 1.0;
        public bool Severe { get; set; } = false;
    }

    private class PriorityThresholdConfig
    {
        public ThresholdEntry? Critical { get; set; }
        public ThresholdEntry? High { get; set; }
        public ThresholdEntry? Medium { get; set; }
    }

    private class ThresholdEntry
    {
        public double MinScore { get; set; }
        public bool RequireSevere { get; set; }
    }

    // ── Structured data models (supports both old flat and new nested) ──

    private class StructuredData
    {
        // Old flat format fields
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public PeopleCount? PeopleCount { get; set; }
        public List<InjuredPerson>? InjuredPersons { get; set; }
        // New nested format fields
        public Incident? Incident { get; set; }
        public List<Victim>? Victims { get; set; }
    }

    private class Incident
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public PeopleCount? PeopleCount { get; set; }
    }

    private class Victim
    {
        public string? PersonType { get; set; }
        public VictimIncidentStatus? IncidentStatus { get; set; }
    }

    private class VictimIncidentStatus
    {
        public bool? IsInjured { get; set; }
        public string? Severity { get; set; }
        public List<string>? MedicalIssues { get; set; }
    }

    private class PeopleCount
    {
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public int? Elderly { get; set; }
    }

    private class InjuredPerson
    {
        public string? PersonType { get; set; }
        public List<string>? MedicalIssues { get; set; }
        public string? Severity { get; set; }
    }
}
