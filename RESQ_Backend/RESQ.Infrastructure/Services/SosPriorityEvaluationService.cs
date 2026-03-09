using System.Text.Json;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;

namespace RESQ.Infrastructure.Services;

public class SosPriorityEvaluationService : ISosPriorityEvaluationService
{
    private const string RULE_VERSION = "3.0";

    // ── issueWeight: camelCase & snake_case variants both supported ──
    private static readonly Dictionary<string, double> IssueWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        // Weight 5: life-threatening
        ["unconscious"]          = 5,
        ["drowning"]             = 5,
        ["breathingDifficulty"]  = 5,
        ["breathing_difficulty"] = 5,
        ["chestPainStroke"]      = 5,
        ["chest_pain_stroke"]    = 5,
        // Weight 4: severe
        ["severelyBleeding"]     = 4,
        ["severely_bleeding"]    = 4,
        ["bleeding"]             = 4,
        ["burns"]                = 4,
        ["headInjury"]           = 4,
        ["head_injury"]          = 4,
        ["cannotMove"]           = 4,
        ["cannot_move"]          = 4,
        // Weight 3: moderate
        ["highFever"]            = 3,
        ["high_fever"]           = 3,
        ["dehydration"]          = 3,
        ["fracture"]             = 3,
        ["infantNeedsMilk"]      = 3,
        ["infant_needs_milk"]    = 3,
        ["lostParent"]           = 3,
        ["lost_parent"]          = 3,
        // Weight 2: mild
        ["chronicDisease"]       = 2,
        ["chronic_disease"]      = 2,
        ["confusion"]            = 2,
        ["needsMedicalDevice"]   = 2,
        ["needs_medical_device"] = 2,
        // Weight 1: other
        ["other"]                = 1,
    };

    // medicalSevere triggers: unconscious, drowning, breathingDifficulty, chestPainStroke, severelyBleeding
    private static readonly HashSet<string> MedicalSevereIssues = new(StringComparer.OrdinalIgnoreCase)
    {
        "unconscious",
        "drowning",
        "breathingDifficulty", "breathing_difficulty",
        "chestPainStroke",     "chest_pain_stroke",
        "severelyBleeding",    "severely_bleeding"
    };

    // ageWeight multiplier by personType
    private static double GetAgeWeight(string? personType) => personType?.ToLowerInvariant() switch
    {
        "child" or "trẻ em"                     => 1.4,
        "elderly" or "elder" or "người già"     => 1.3,
        _                                        => 1.0   // adult
    };

    // requestTypeScore: rescue=30, relief=20, else=10
    private static int GetRequestTypeScore(string? sosType) => sosType?.ToLowerInvariant() switch
    {
        "rescue" => 30,
        "relief" => 20,
        _        => 10
    };

    // situationMultiplier + situationSevere flag
    private static (double multiplier, bool severe) GetSituationMultiplier(string? situation) =>
        situation?.ToLowerInvariant() switch
        {
            "flooding"  or "flood"                                   => (1.5, true),
            "building_collapse" or "collapsed" or "collapse"         => (1.5, true),
            "trapped"                                                 => (1.3, false),
            "danger_zone" or "dangerous_area" or "dangerous"         => (1.3, false),
            "cannot_move" or "cannotmove"                            => (1.2, false),
            _                                                         => (1.0, false)
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

        // ── 1. requestTypeScore ──
        int requestTypeScore = GetRequestTypeScore(sosType);

        // ── 2. situationMultiplier + situationSevere flag ──
        var (situationMultiplier, situationSevere) = GetSituationMultiplier(data?.Situation);

        // ── 3. medicalScore = Σ per-person: (Σ issueWeight) × ageWeight ──
        double medicalScore = 0;
        bool medicalSevere = false;
        if (data?.InjuredPersons is { Count: > 0 } persons)
        {
            foreach (var person in persons)
            {
                var issues = person.MedicalIssues ?? [];
                double issueScore = issues.Sum(k => IssueWeight.GetValueOrDefault(k, 1));
                double ageMultiplier = GetAgeWeight(person.PersonType);
                medicalScore += issueScore * ageMultiplier;

                if (!medicalSevere && issues.Any(k => MedicalSevereIssues.Contains(k)))
                    medicalSevere = true;
            }
        }

        // ── 4. Assemble scores ──
        // EnvironmentScore = requestTypeScore
        // MedicalScore     = Σ (issueScore × ageWeight) across all injured persons
        // TotalScore       = (requestTypeScore + medicalScore) × situationMultiplier
        evaluation.EnvironmentScore = requestTypeScore;
        evaluation.MedicalScore     = medicalScore;
        evaluation.InjuryScore      = 0;
        evaluation.FoodScore        = 0;
        evaluation.MobilityScore    = 0;

        evaluation.TotalScore = (requestTypeScore + medicalScore) * situationMultiplier;

        // ── 5. Triage P1–P4 (P1/P2 require a severe flag) ──
        bool isSevere = medicalSevere || situationSevere;
        evaluation.PriorityLevel = DeterminePriorityLevel(evaluation.TotalScore, isSevere);

        evaluation.ItemsNeeded = DetermineItemsNeeded(data, sosType);

        return evaluation;
    }

    // P1 (Critical) >= 70 + severe
    // P2 (High)     >= 45 + severe
    // P3 (Medium)   >= 25
    // P4 (Low)       < 25
    private static SosPriorityLevel DeterminePriorityLevel(double totalScore, bool isSevere)
    {
        if (totalScore >= 70 && isSevere) return SosPriorityLevel.Critical;
        if (totalScore >= 45 && isSevere) return SosPriorityLevel.High;
        if (totalScore >= 25)             return SosPriorityLevel.Medium;
        return SosPriorityLevel.Low;
    }

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
