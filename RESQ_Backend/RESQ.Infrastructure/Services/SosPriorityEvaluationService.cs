using System.Text.Json;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Infrastructure.Services;

public class SosPriorityEvaluationService : ISosPriorityEvaluationService
{
    private const string RULE_VERSION = "2.0";

    // Medical issue severity values (camelCase and snake_case variants both supported)
    private static readonly Dictionary<string, int> IssueSeverity = new(StringComparer.OrdinalIgnoreCase)
    {
        // Injury group
        ["bleeding"] = 4,
        ["severelyBleeding"] = 4,
        ["severely_bleeding"] = 4,
        ["fracture"] = 3,
        ["headInjury"] = 4,
        ["head_injury"] = 4,
        ["burns"] = 4,
        // Danger group
        ["unconscious"] = 5,
        ["breathingDifficulty"] = 5,
        ["breathing_difficulty"] = 5,
        ["chestPainStroke"] = 5,
        ["chest_pain_stroke"] = 5,
        ["cannotMove"] = 4,
        ["cannot_move"] = 4,
        ["drowning"] = 5,
        // Special group
        ["highFever"] = 3,
        ["high_fever"] = 3,
        ["dehydration"] = 3,
        ["infantNeedsMilk"] = 3,
        ["infant_needs_milk"] = 3,
        ["lostParent"] = 3,
        ["lost_parent"] = 3,
        ["chronicDisease"] = 2,
        ["chronic_disease"] = 2,
        ["confusion"] = 2,
        ["needsMedicalDevice"] = 2,
        ["needs_medical_device"] = 2,
        // Other
        ["other"] = 1
    };

    // severity field value → score: critical=5, moderate=3, mild=1
    private static int GetSeverityScore(string? severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => 5,
        "moderate" => 3,
        "mild" => 1,
        _ => 1
    };

    // sosType base score: rescue=50, relief=20, anything else=10
    private static int GetBaseScore(string? sosType) => sosType?.ToLowerInvariant() switch
    {
        "rescue" => 50,
        "relief" => 20,
        _ => 10
    };

    public SosRuleEvaluationModel Evaluate(int sosRequestId, string? structuredDataJson, string? sosType)
    {
        var evaluation = new SosRuleEvaluationModel
        {
            SosRequestId = sosRequestId,
            RuleVersion = RULE_VERSION,
            CreatedAt = DateTime.UtcNow
        };

        StructuredData? data = null;
        if (!string.IsNullOrWhiteSpace(structuredDataJson))
        {
            try
            {
                data = JsonSerializer.Deserialize<StructuredData>(structuredDataJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
            }
            catch { }
        }

        // ── 1. Base score (from SOS type) + dangerous situation bonus ──
        int baseScore = GetBaseScore(sosType);
        var situation = data?.Situation?.ToLowerInvariant();
        int dangerBonus = situation is "building_collapse" or "flooding" ? 20 : 0;

        // ── 2. Demographic score: children×3 + elderly×2 ──
        int children = data?.PeopleCount?.Child ?? 0;
        int elderly = data?.PeopleCount?.Elderly ?? 0;
        int demographicScore = children * 3 + elderly * 2;

        // ── 3. Medical priority score: Σ per-person (severity×2 + Σ issueSeverity) ──
        int medicalPriorityScore = 0;
        int injuredCount = 0;
        if (data?.InjuredPersons is { Count: > 0 } persons)
        {
            injuredCount = persons.Count;
            foreach (var person in persons)
            {
                int severityScore = GetSeverityScore(person.Severity);
                int issueSum = person.MedicalIssues?.Sum(k => IssueSeverity.GetValueOrDefault(k, 1)) ?? 0;
                medicalPriorityScore += severityScore * 2 + issueSum;
            }
        }

        // ── 4. Assemble score components ──
        // EnvironmentScore = base + danger bonus
        // MedicalScore     = medicalPriorityScore × 5
        // InjuryScore      = injuredCount × 4
        // FoodScore        = demographic (children/elderly)
        // MobilityScore    = 0 (not used in this formula)
        evaluation.EnvironmentScore = baseScore + dangerBonus;
        evaluation.MedicalScore = medicalPriorityScore * 5;
        evaluation.InjuryScore = injuredCount * 4;
        evaluation.FoodScore = demographicScore;
        evaluation.MobilityScore = 0;

        evaluation.TotalScore = evaluation.EnvironmentScore
                              + evaluation.MedicalScore
                              + evaluation.InjuryScore
                              + evaluation.FoodScore;

        evaluation.PriorityLevel = DeterminePriorityLevel(evaluation.TotalScore);
        evaluation.ItemsNeeded = DetermineItemsNeeded(data, sosType);

        return evaluation;
    }

    // Thresholds calibrated for the unbounded score scale
    // Example from spec: rescue+flooding+1child+1elderly+critical(unconscious+fracture) = 169 → Critical
    private static SosPriorityLevel DeterminePriorityLevel(double totalScore) => totalScore switch
    {
        >= 150 => SosPriorityLevel.Critical,
        >= 70  => SosPriorityLevel.High,
        >= 30  => SosPriorityLevel.Medium,
        _      => SosPriorityLevel.Low
    };

    private static string? DetermineItemsNeeded(StructuredData? data, string? sosType)
    {
        var items = new List<string>();

        var allIssues = data?.InjuredPersons?
            .SelectMany(p => p.MedicalIssues ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        if (data?.InjuredPersons?.Count > 0 || data?.HasInjured == true)
        {
            items.Add("FIRST_AID_KIT");
            items.Add("MEDICAL_SUPPLIES");
        }

        if (allIssues.Contains("bleeding") || allIssues.Contains("severelyBleeding") || allIssues.Contains("severely_bleeding"))
        {
            items.Add("BANDAGES");
            items.Add("BLOOD_CLOTTING_AGENTS");
        }

        var situation = data?.Situation?.ToLowerInvariant();

        if (situation is "flooding")
        {
            items.Add("LIFE_JACKET");
            items.Add("RESCUE_BOAT");
            items.Add("ROPE");
        }

        if (situation is "trapped" or "building_collapse")
        {
            items.Add("ROPE");
            items.Add("RESCUE_EQUIPMENT");
        }

        if (situation is "fire")
        {
            items.Add("FIRE_EXTINGUISHER");
            items.Add("PROTECTIVE_GEAR");
        }

        if (data?.PeopleCount is { } pc)
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

    private class StructuredData
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public PeopleCount? PeopleCount { get; set; }
        public List<InjuredPerson>? InjuredPersons { get; set; }
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
