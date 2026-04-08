using System.Text.Json;
using RESQ.Application.Common;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;

namespace RESQ.Infrastructure.Services;

public class SosPriorityEvaluationService(ISosPriorityRuleConfigRepository ruleConfigRepository) : ISosPriorityEvaluationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ISosPriorityRuleConfigRepository _ruleConfigRepository = ruleConfigRepository;

    public async Task<SosRuleEvaluationModel> EvaluateAsync(
        int sosRequestId,
        string? structuredDataJson,
        string? sosType,
        CancellationToken cancellationToken = default)
    {
        var configModel = await _ruleConfigRepository.GetAsync(cancellationToken);
        var config = SosPriorityRuleConfigSupport.FromModel(configModel);

        var structuredData = DeserializeStructuredData(structuredDataJson);
        var situationKey = NormalizeSituation(structuredData?.Incident?.Situation ?? structuredData?.Situation);
        var situationMultiplier = ResolveSituationMultiplier(situationKey, config);
        var situationSevere = SosPriorityRuleConfigSupport.IsSevereSituation(situationKey);

        var peopleSummary = ResolvePeopleSummary(structuredData);
        var injuredPeople = BuildInjuredPeople(structuredData);

        var medicalIssueBreakdown = new List<SosMedicalIssueBreakdownItem>();
        var medicalScore = 0d;
        var medicalSevere = false;

        foreach (var injuredPerson in injuredPeople)
        {
            var issueScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var issue in injuredPerson.MedicalIssues)
            {
                var issueKey = SosPriorityRuleConfigSupport.NormalizeKey(issue);
                var weight = ResolveMedicalIssueWeight(config, issueKey);
                issueScores[issueKey] = weight;
                if (!medicalSevere && SosPriorityRuleConfigSupport.IsSevereMedicalIssue(issueKey))
                {
                    medicalSevere = true;
                }
            }

            var issueWeightSum = issueScores.Values.Sum();
            var ageWeight = ResolveAgeWeight(config, injuredPerson.PersonType);
            var total = issueWeightSum * ageWeight;
            medicalScore += total;
            medicalIssueBreakdown.Add(new SosMedicalIssueBreakdownItem
            {
                PersonType = NormalizePersonType(injuredPerson.PersonType),
                IssueScores = issueScores,
                IssueWeightSum = issueWeightSum,
                AgeWeight = ageWeight,
                Total = total
            });
        }

        var waterDuration = NormalizeWaterDuration(structuredData?.GroupNeeds?.Water?.Duration, config);
        var foodDuration = NormalizeFoodDuration(structuredData?.GroupNeeds?.Food?.Duration, config);

        var waterUrgencyScore = ResolveMappedScore(config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore, waterDuration);
        var foodUrgencyScore = ResolveMappedScore(config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore, foodDuration);

        var blanketsSelected = IsBlanketSelected(structuredData);
        var areBlanketsEnough = structuredData?.GroupNeeds?.Blanket?.AreBlanketsEnough;
        var blanketRequestCount = ResolveBlanketRequestCount(structuredData, config, peopleSummary.TotalPeople, blanketsSelected);
        var blanketUrgencyScore = ResolveBlanketUrgencyScore(
            config,
            blanketsSelected,
            areBlanketsEnough,
            blanketRequestCount,
            peopleSummary.TotalPeople);

        var clothingSelected = IsClothingSelected(structuredData);
        var clothingNeededPeopleCount = ResolveClothingNeededPeopleCount(structuredData);
        var clothingUrgencyScore = ResolveClothingUrgencyScore(
            config,
            clothingSelected,
            clothingNeededPeopleCount,
            peopleSummary.TotalPeople);

        var supplyUrgencyScore = waterUrgencyScore + foodUrgencyScore + blanketUrgencyScore + clothingUrgencyScore;
        var vulnerabilityRaw = (peopleSummary.ChildCount * config.ReliefScore.VulnerabilityScore.VulnerabilityRaw.ChildPerPerson)
            + (peopleSummary.ElderlyCount * config.ReliefScore.VulnerabilityScore.VulnerabilityRaw.ElderlyPerPerson)
            + (peopleSummary.HasPregnantAny ? config.ReliefScore.VulnerabilityScore.VulnerabilityRaw.HasPregnantAny : 0d);
        var vulnerabilityScore = Math.Min(vulnerabilityRaw, supplyUrgencyScore * config.ReliefScore.VulnerabilityScore.CapRatio);
        var reliefScore = supplyUrgencyScore + vulnerabilityScore;
        var totalScore = Math.Round((medicalScore + reliefScore) * situationMultiplier, 0, MidpointRounding.AwayFromZero);
        var hasSevereFlag = medicalSevere || situationSevere;
        var priorityLevel = SosPriorityRuleConfigSupport.DeterminePriorityLevel(totalScore, hasSevereFlag, config);

        var details = new SosPriorityEvaluationDetails
        {
            NormalizedSituation = situationKey,
            TotalPeople = peopleSummary.TotalPeople,
            InjuredPeopleCount = injuredPeople.Count,
            MedicalScore = medicalScore,
            ReliefScore = reliefScore,
            SupplyUrgencyScore = supplyUrgencyScore,
            WaterUrgencyScore = waterUrgencyScore,
            FoodUrgencyScore = foodUrgencyScore,
            BlanketUrgencyScore = blanketUrgencyScore,
            ClothingUrgencyScore = clothingUrgencyScore,
            VulnerabilityRaw = vulnerabilityRaw,
            VulnerabilityScore = vulnerabilityScore,
            SituationMultiplier = situationMultiplier,
            MedicalSevereFlag = medicalSevere,
            SituationSevereFlag = situationSevere,
            HasSevereFlag = hasSevereFlag,
            WaterDuration = waterDuration,
            FoodDuration = foodDuration,
            BlanketsSelected = blanketsSelected,
            AreBlanketsEnough = areBlanketsEnough,
            BlanketRequestCount = blanketRequestCount,
            ClothingSelected = clothingSelected,
            ClothingNeededPeopleCount = clothingNeededPeopleCount,
            ChildrenCount = peopleSummary.ChildCount,
            ElderlyCount = peopleSummary.ElderlyCount,
            HasPregnantAny = peopleSummary.HasPregnantAny,
            MedicalIssueBreakdown = medicalIssueBreakdown
        };

        return new SosRuleEvaluationModel
        {
            SosRequestId = sosRequestId,
            MedicalScore = medicalScore,
            InjuryScore = supplyUrgencyScore,
            MobilityScore = vulnerabilityScore,
            EnvironmentScore = situationMultiplier,
            FoodScore = reliefScore,
            TotalScore = totalScore,
            PriorityLevel = priorityLevel,
            RuleVersion = string.IsNullOrWhiteSpace(config.ConfigVersion) ? "SOS_PRIORITY_V1" : config.ConfigVersion,
            ItemsNeeded = DetermineItemsNeeded(structuredData, sosType, blanketsSelected, clothingSelected),
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static StructuredData? DeserializeStructuredData(string? structuredDataJson)
    {
        if (string.IsNullOrWhiteSpace(structuredDataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StructuredData>(structuredDataJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static double ResolveMedicalIssueWeight(SosPriorityRuleConfigDocument config, string issueKey)
    {
        return config.MedicalScore.MedicalIssueSeverity.TryGetValue(issueKey, out var configuredWeight)
            ? configuredWeight
            : config.MedicalScore.MedicalIssueSeverity.GetValueOrDefault("OTHER", 1d);
    }

    private static double ResolveAgeWeight(SosPriorityRuleConfigDocument config, string? personType)
    {
        var normalizedPersonType = NormalizePersonType(personType);
        return config.MedicalScore.AgeWeights.TryGetValue(normalizedPersonType, out var ageWeight)
            ? ageWeight
            : config.MedicalScore.AgeWeights.GetValueOrDefault("ADULT", 1d);
    }

    private static int ResolveMappedScore(IReadOnlyDictionary<string, int> mapping, string normalizedKey)
    {
        return mapping.TryGetValue(normalizedKey, out var score)
            ? score
            : mapping.GetValueOrDefault("NOT_SELECTED", 0);
    }

    private static double ResolveSituationMultiplier(string normalizedSituation, SosPriorityRuleConfigDocument config)
    {
        if (!string.IsNullOrWhiteSpace(normalizedSituation)
            && config.SituationMultiplier.TryGetValue(normalizedSituation, out var configured))
        {
            return configured;
        }

        if (config.SituationMultiplier.TryGetValue("OTHER", out var other))
        {
            return other;
        }

        return config.SituationMultiplier.GetValueOrDefault("DEFAULT_WHEN_NULL", 1d);
    }

    private static PeopleSummary ResolvePeopleSummary(StructuredData? data)
    {
        var adult = Math.Max(0, data?.Incident?.PeopleCount?.Adult ?? data?.PeopleCount?.Adult ?? 0);
        var child = Math.Max(0, data?.Incident?.PeopleCount?.Child ?? data?.PeopleCount?.Child ?? 0);
        var elderly = Math.Max(0, data?.Incident?.PeopleCount?.Elderly ?? data?.PeopleCount?.Elderly ?? 0);
        var hasPregnantAny = data?.Incident?.HasPregnantAny ?? false;

        if (adult + child + elderly == 0 && data?.Victims is { Count: > 0 })
        {
            adult = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "ADULT");
            child = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "CHILD");
            elderly = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "ELDERLY");
        }

        return new PeopleSummary(adult, child, elderly, hasPregnantAny);
    }

    private static List<InjuredPerson> BuildInjuredPeople(StructuredData? data)
    {
        if (data?.Victims is { Count: > 0 })
        {
            return data.Victims
                .Where(v => v.IncidentStatus?.IsInjured == true
                    || v.IncidentStatus?.MedicalIssues is { Count: > 0 })
                .Select(v => new InjuredPerson
                {
                    PersonType = v.PersonType,
                    MedicalIssues = (v.IncidentStatus?.MedicalIssues ?? [])
                        .Select(SosPriorityRuleConfigSupport.NormalizeKey)
                        .Where(issue => !string.IsNullOrWhiteSpace(issue))
                        .ToList()
                })
                .ToList();
        }

        return data?.InjuredPersons?
            .Select(person => new InjuredPerson
            {
                PersonType = person.PersonType,
                MedicalIssues = (person.MedicalIssues ?? [])
                    .Select(SosPriorityRuleConfigSupport.NormalizeKey)
                    .Where(issue => !string.IsNullOrWhiteSpace(issue))
                    .ToList()
            })
            .ToList() ?? [];
    }

    private static int? ResolveBlanketRequestCount(
        StructuredData? data,
        SosPriorityRuleConfigDocument config,
        int totalPeople,
        bool blanketsSelected)
    {
        var requestCount = data?.GroupNeeds?.Blanket?.RequestCount;
        if (requestCount.HasValue)
        {
            return requestCount.Value;
        }

        if (!blanketsSelected || data?.GroupNeeds?.Blanket?.AreBlanketsEnough != false)
        {
            return null;
        }

        return totalPeople <= 0 ? config.UiConstraints.BlanketRequestCountDefault : config.UiConstraints.BlanketRequestCountDefault;
    }

    private static int ResolveBlanketUrgencyScore(
        SosPriorityRuleConfigDocument config,
        bool blanketsSelected,
        bool? areBlanketsEnough,
        int? blanketRequestCount,
        int totalPeople)
    {
        var rule = config.ReliefScore.SupplyUrgencyScore.BlanketUrgencyScore;
        if (rule.ApplyOnlyWhenSupplySelected && !blanketsSelected)
        {
            return rule.NoneOrNotSelectedScore;
        }

        if (rule.ApplyOnlyWhenAreBlanketsEnoughIsFalse && areBlanketsEnough != false)
        {
            return rule.NoneOrNotSelectedScore;
        }

        if (!blanketRequestCount.HasValue || blanketRequestCount.Value <= 0 || totalPeople <= 0)
        {
            return rule.NoneOrNotSelectedScore;
        }

        var requestedCount = blanketRequestCount.Value;
        if (requestedCount == 1)
        {
            return rule.RequestedCountEquals1Score;
        }

        if (IsMoreThanHalf(requestedCount, totalPeople, rule.HalfPeopleOperator))
        {
            return rule.RequestedCountMoreThanHalfPeopleScore;
        }

        if (requestedCount >= 2)
        {
            return rule.RequestedCountBetween2AndHalfPeopleScore;
        }

        return rule.NoneOrNotSelectedScore;
    }

    private static int? ResolveClothingNeededPeopleCount(StructuredData? data)
    {
        var explicitCount = data?.GroupNeeds?.Clothing?.NeededPeopleCount;
        if (explicitCount.HasValue)
        {
            return explicitCount.Value;
        }

        if (data?.Victims is not { Count: > 0 })
        {
            return null;
        }

        var inferredCount = data.Victims.Count(v => v.PersonalNeeds?.Clothing?.Needed == true);
        return inferredCount > 0 ? inferredCount : null;
    }

    private static int ResolveClothingUrgencyScore(
        SosPriorityRuleConfigDocument config,
        bool clothingSelected,
        int? clothingNeededPeopleCount,
        int totalPeople)
    {
        var rule = config.ReliefScore.SupplyUrgencyScore.ClothingUrgencyScore;
        if (rule.ApplyOnlyWhenSupplySelected && !clothingSelected)
        {
            return rule.NoneOrNotSelectedScore;
        }

        if (!clothingNeededPeopleCount.HasValue || clothingNeededPeopleCount.Value <= 0 || totalPeople <= 0)
        {
            return rule.NoneOrNotSelectedScore;
        }

        if (clothingNeededPeopleCount.Value == 1)
        {
            return rule.NeededPeopleEquals1Score;
        }

        if (IsMoreThanHalf(clothingNeededPeopleCount.Value, totalPeople, rule.HalfPeopleOperator))
        {
            return rule.NeededPeopleMoreThanHalfPeopleScore;
        }

        if (clothingNeededPeopleCount.Value >= 2)
        {
            return rule.NeededPeopleBetween2AndHalfPeopleScore;
        }

        return rule.NoneOrNotSelectedScore;
    }

    private static bool IsMoreThanHalf(int value, int totalPeople, string? halfOperator)
    {
        var half = totalPeople / 2d;
        return string.Equals((halfOperator ?? ">=").Trim(), ">=", StringComparison.Ordinal)
            ? value >= half
            : value > half;
    }

    private static bool IsBlanketSelected(StructuredData? data)
    {
        return HasSupply(data?.GroupNeeds?.Supplies, "BLANKET")
            || data?.GroupNeeds?.Blanket is not null;
    }

    private static bool IsClothingSelected(StructuredData? data)
    {
        return HasSupply(data?.GroupNeeds?.Supplies, "CLOTHING")
            || data?.GroupNeeds?.Clothing is not null
            || data?.Victims?.Any(v => v.PersonalNeeds?.Clothing?.Needed == true) == true;
    }

    private static bool HasSupply(List<string>? supplies, string expectedSupply)
    {
        if (supplies is not { Count: > 0 })
        {
            return false;
        }

        var normalizedExpectedSupply = SosPriorityRuleConfigSupport.NormalizeKey(expectedSupply);
        return supplies
            .Select(SosPriorityRuleConfigSupport.NormalizeKey)
            .Any(supply => supply == normalizedExpectedSupply || supply == normalizedExpectedSupply + "S");
    }

    private static string NormalizeSituation(string? situation)
    {
        var normalized = SosPriorityRuleConfigSupport.NormalizeKey(situation);
        return normalized switch
        {
            "BUILDING_COLLAPSE" or "COLLAPSED" or "COLLAPSE" => "COLLAPSED",
            "FLOOD" => "FLOODING",
            "DANGEROUS_AREA" or "DANGEROUS" => "DANGER_ZONE",
            "CANNOTMOVE" => "CANNOT_MOVE",
            _ when string.IsNullOrWhiteSpace(normalized) => "DEFAULT_WHEN_NULL",
            _ => normalized
        };
    }

    private static string NormalizePersonType(string? personType)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(personType) switch
        {
            "TRE_EM" or "CHILDREN" => "CHILD",
            "ELDER" or "NGUOI_GIA" => "ELDERLY",
            var normalized when string.IsNullOrWhiteSpace(normalized) => "ADULT",
            var normalized => normalized
        };
    }

    private static string NormalizeWaterDuration(string? duration, SosPriorityRuleConfigDocument config)
    {
        var normalized = SosPriorityRuleConfigSupport.NormalizeKey(duration);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "NOT_SELECTED";
        }

        if (ContainsConfiguredOption(config.UiOptions.WaterDuration, normalized))
        {
            return normalized;
        }

        return normalized switch
        {
            var value when value.Contains("UNDER_6") || value.Contains("LESS_THAN_6") => "UNDER_6H",
            var value when value.Contains("6_TO_12") => "6_TO_12H",
            var value when value.Contains("12_TO_24") => "12_TO_24H",
            var value when value.Contains("1_TO_2_DAYS") => "1_TO_2_DAYS",
            var value when value.Contains("OVER_2_DAYS") || value.Contains("3_DAYS") || value.Contains("MORE_THAN_2_DAYS") => "OVER_2_DAYS",
            _ => "NOT_SELECTED"
        };
    }

    private static string NormalizeFoodDuration(string? duration, SosPriorityRuleConfigDocument config)
    {
        var normalized = SosPriorityRuleConfigSupport.NormalizeKey(duration);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "NOT_SELECTED";
        }

        if (ContainsConfiguredOption(config.UiOptions.FoodDuration, normalized))
        {
            return normalized;
        }

        return normalized switch
        {
            var value when value.Contains("UNDER_12") || value.Contains("LESS_THAN_12") => "UNDER_12H",
            var value when value.Contains("12_TO_24") => "12_TO_24H",
            var value when value.Contains("1_TO_2_DAYS") => "1_TO_2_DAYS",
            var value when value.Contains("2_TO_3_DAYS") || value.Contains("3_DAYS") => "2_TO_3_DAYS",
            var value when value.Contains("OVER_3_DAYS") || value.Contains("MORE_THAN_3_DAYS") => "OVER_3_DAYS",
            _ => "NOT_SELECTED"
        };
    }

    private static bool ContainsConfiguredOption(IEnumerable<string> configuredOptions, string normalizedValue)
    {
        return configuredOptions
            .Select(SosPriorityRuleConfigSupport.NormalizeKey)
            .Contains(normalizedValue, StringComparer.OrdinalIgnoreCase);
    }

    private static string? DetermineItemsNeeded(StructuredData? data, string? sosType, bool blanketsSelected, bool clothingSelected)
    {
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var peopleSummary = ResolvePeopleSummary(data);
        var situation = NormalizeSituation(data?.Incident?.Situation ?? data?.Situation);
        var injuredPeople = BuildInjuredPeople(data);

        if (injuredPeople.Count > 0 || data?.Incident?.HasInjured == true || data?.HasInjured == true)
        {
            items.Add("FIRST_AID_KIT");
            items.Add("MEDICAL_SUPPLIES");
        }

        if (injuredPeople.SelectMany(person => person.MedicalIssues).Any(issue => issue is "BLEEDING" or "SEVERELY_BLEEDING"))
        {
            items.Add("BANDAGES");
            items.Add("BLOOD_CLOTTING_AGENTS");
        }

        if (situation == "FLOODING")
        {
            items.Add("LIFE_JACKET");
            items.Add("RESCUE_BOAT");
            items.Add("ROPE");
        }

        if (situation is "TRAPPED" or "COLLAPSED")
        {
            items.Add("ROPE");
            items.Add("RESCUE_EQUIPMENT");
        }

        if (peopleSummary.TotalPeople > 0)
        {
            items.Add("WATER");
            items.Add("FOOD_RATIONS");
        }

        if (blanketsSelected)
        {
            items.Add("BLANKETS");
        }

        if (clothingSelected)
        {
            items.Add("CLOTHING");
        }

        if (string.Equals(SosPriorityRuleConfigSupport.NormalizeKey(sosType), "EVACUATION", StringComparison.OrdinalIgnoreCase))
        {
            items.Add("TRANSPORT_VEHICLE");
            items.Add("STRETCHER");
        }

        return items.Count > 0 ? JsonSerializer.Serialize(items.OrderBy(item => item)) : null;
    }

    private sealed class StructuredData
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public PeopleCount? PeopleCount { get; set; }
        public List<InjuredPerson>? InjuredPersons { get; set; }
        public Incident? Incident { get; set; }
        public GroupNeeds? GroupNeeds { get; set; }
        public List<Victim>? Victims { get; set; }
    }

    private sealed class Incident
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public bool? HasPregnantAny { get; set; }
        public PeopleCount? PeopleCount { get; set; }
    }

    private sealed class GroupNeeds
    {
        public List<string>? Supplies { get; set; }
        public WaterNeed? Water { get; set; }
        public FoodNeed? Food { get; set; }
        public BlanketNeed? Blanket { get; set; }
        public ClothingNeed? Clothing { get; set; }
    }

    private sealed class WaterNeed
    {
        public string? Duration { get; set; }
    }

    private sealed class FoodNeed
    {
        public string? Duration { get; set; }
    }

    private sealed class BlanketNeed
    {
        public bool? AreBlanketsEnough { get; set; }
        public int? RequestCount { get; set; }
    }

    private sealed class ClothingNeed
    {
        public string? Status { get; set; }
        public int? NeededPeopleCount { get; set; }
    }

    private sealed class Victim
    {
        public string? PersonType { get; set; }
        public VictimIncidentStatus? IncidentStatus { get; set; }
        public VictimPersonalNeeds? PersonalNeeds { get; set; }
    }

    private sealed class VictimIncidentStatus
    {
        public bool? IsInjured { get; set; }
        public List<string>? MedicalIssues { get; set; }
    }

    private sealed class VictimPersonalNeeds
    {
        public VictimClothingNeed? Clothing { get; set; }
    }

    private sealed class VictimClothingNeed
    {
        public bool? Needed { get; set; }
    }

    private sealed class PeopleCount
    {
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public int? Elderly { get; set; }
    }

    private sealed class InjuredPerson
    {
        public string? PersonType { get; set; }
        public List<string> MedicalIssues { get; set; } = [];
    }

    private readonly record struct PeopleSummary(int AdultCount, int ChildCount, int ElderlyCount, bool HasPregnantAny)
    {
        public int TotalPeople => AdultCount + ChildCount + ElderlyCount;
    }
}
