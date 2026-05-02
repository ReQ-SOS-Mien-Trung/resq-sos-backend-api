using System.Text.Json;
using RESQ.Application.Common;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;

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
        return await EvaluateWithConfigAsync(sosRequestId, structuredDataJson, sosType, configModel, cancellationToken);
    }

    public Task<SosRuleEvaluationModel> EvaluateWithConfigAsync(
        int sosRequestId,
        string? structuredDataJson,
        string? sosType,
        SosPriorityRuleConfigModel? configModel,
        CancellationToken cancellationToken = default)
    {
        var config = SosPriorityRuleConfigSupport.FromModel(configModel);
        var ruleVersion = string.IsNullOrWhiteSpace(config.ConfigVersion) ? "SOS_PRIORITY_V2" : config.ConfigVersion;

        var structuredData = DeserializeStructuredData(structuredDataJson);
        var situationKey = NormalizeSituation(structuredData?.Incident?.Situation ?? structuredData?.Situation);
        var situationMultiplier = ResolveSituationMultiplier(situationKey, config);
        var situationSevere = SosPriorityRuleConfigSupport.IsSevereSituation(situationKey);
        var requestTypeScore = ResolveRequestTypeScore(config, sosType);

        var peopleSummary = ResolvePeopleSummary(structuredData);
        var injuredPeople = BuildInjuredPeople(structuredData);

        var medicalIssueBreakdown = new List<SosMedicalIssueBreakdownItem>();
        var medicalIssueScore = 0d;
        var victimSeverityScore = 0d;
        var medicalScore = 0d;
        var medicalSevere = false;
        var criticalSeverity = false;

        foreach (var injuredPerson in injuredPeople)
        {
            var issueScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var issue in injuredPerson.MedicalIssues)
            {
                var issueKey = SosPriorityRuleConfigSupport.NormalizeKey(issue);
                var weight = ResolveMedicalIssueWeight(config, issueKey);
                issueScores[issueKey] = weight;
                if (!medicalSevere && SosPriorityRuleConfigSupport.IsSevereMedicalIssue(issueKey, config))
                {
                    medicalSevere = true;
                }
            }

            var issueWeightSum = issueScores.Values.Sum();
            var ageWeight = ResolveAgeWeight(config, injuredPerson.PersonType);
            var issueTotal = issueWeightSum * ageWeight;
            var normalizedSeverity = NormalizeVictimSeverity(injuredPerson.Severity);
            var severityScore = ResolveVictimSeverityScore(config, normalizedSeverity);
            var total = issueTotal + severityScore;
            medicalIssueScore += issueTotal;
            victimSeverityScore += severityScore;
            medicalScore += total;
            criticalSeverity = criticalSeverity || normalizedSeverity == "CRITICAL";
            medicalIssueBreakdown.Add(new SosMedicalIssueBreakdownItem
            {
                PersonType = NormalizePersonType(injuredPerson.PersonType),
                IssueScores = issueScores,
                IssueWeightSum = issueWeightSum,
                AgeWeight = ageWeight,
                VictimSeverity = string.IsNullOrWhiteSpace(normalizedSeverity) ? null : normalizedSeverity,
                VictimSeverityScore = severityScore,
                Total = total
            });
        }

        var medicineUrgencyScore = ResolveMedicineUrgencyScore(config, structuredData?.GroupNeeds?.Medicine);
        var urgentMedicine = HasUrgentMedicineNeed(structuredData?.GroupNeeds?.Medicine, medicineUrgencyScore);
        medicalScore += medicineUrgencyScore;

        var waterDuration = NormalizeWaterDuration(
            structuredData?.GroupNeeds?.Water?.Duration ?? structuredData?.SupplyDetails?.WaterDuration,
            config);
        var foodDuration = NormalizeFoodDuration(
            structuredData?.GroupNeeds?.Food?.Duration ?? structuredData?.SupplyDetails?.FoodDuration,
            config);

        var waterUrgencyScore = ResolveMappedScore(config.ReliefScore.SupplyUrgencyScore.WaterUrgencyScore, waterDuration);
        var foodUrgencyScore = ResolveMappedScore(config.ReliefScore.SupplyUrgencyScore.FoodUrgencyScore, foodDuration);

        var blanketsSelected = IsBlanketSelected(structuredData);
        var areBlanketsEnough = ResolveAreBlanketsEnough(structuredData);
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

        var evaluationContext = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["MEDICAL_SCORE"] = medicalScore,
            ["REQUEST_TYPE_SCORE"] = requestTypeScore,
            ["SUPPLY_URGENCY_SCORE"] = supplyUrgencyScore,
            ["VULNERABILITY_RAW"] = vulnerabilityRaw,
            ["CAP_RATIO"] = config.ReliefScore.VulnerabilityScore.CapRatio,
            ["SITUATION_MULTIPLIER"] = situationMultiplier
        };

        var vulnerabilityScore = SosExpressionEngine.Evaluate(
            config.ReliefScore.VulnerabilityScore.Expression,
            evaluationContext,
            "relief_score.vulnerability_score.expression");
        evaluationContext["VULNERABILITY_SCORE"] = vulnerabilityScore;

        var reliefScore = SosExpressionEngine.Evaluate(
            config.ReliefScore.Expression,
            evaluationContext,
            "relief_score.expression");
        evaluationContext["RELIEF_SCORE"] = reliefScore;
        var reliefPressureMultiplier = ResolveReliefPressureMultiplier(reliefScore);
        evaluationContext["RELIEF_PRESSURE_MULTIPLIER"] = reliefPressureMultiplier;

        var priorityScore = SosExpressionEngine.Evaluate(
            config.PriorityScore.Expression,
            evaluationContext,
            "priority_score.expression");

        var hasSevereFlag = medicalSevere || criticalSeverity || situationSevere;
        var thresholdPriorityLevel = SosPriorityRuleConfigSupport.DeterminePriorityLevel(priorityScore, hasSevereFlag, config);
        var dangerousSituation = IsDangerousSituation(situationKey);
        var hasVulnerablePeople = peopleSummary.ChildCount > 0
            || peopleSummary.ElderlyCount > 0
            || peopleSummary.HasPregnantAny;
        var (priorityLevel, escalationDecision) = ResolvePriorityEscalation(
            thresholdPriorityLevel,
            criticalSeverity,
            medicalSevere,
            dangerousSituation,
            hasVulnerablePeople,
            urgentMedicine,
            supplyUrgencyScore,
            reliefScore,
            areBlanketsEnough);
        var itemsNeeded = DetermineItemsNeeded(structuredData, sosType, blanketsSelected, clothingSelected);

        var rawVariables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["medical_score"] = medicalScore,
            ["medical_issue_score"] = medicalIssueScore,
            ["victim_severity_score"] = victimSeverityScore,
            ["medicine_urgency_score"] = medicineUrgencyScore,
            ["request_type_score"] = requestTypeScore,
            ["supply_urgency_score"] = supplyUrgencyScore,
            ["vulnerability_raw"] = vulnerabilityRaw,
            ["cap_ratio"] = config.ReliefScore.VulnerabilityScore.CapRatio,
            ["situation_multiplier"] = situationMultiplier,
            ["relief_pressure_multiplier"] = reliefPressureMultiplier
        };

        var derivedValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["vulnerability_score"] = vulnerabilityScore,
            ["relief_score"] = reliefScore,
            ["relief_pressure_multiplier"] = reliefPressureMultiplier,
            ["priority_score"] = priorityScore
        };

        var details = new SosPriorityEvaluationDetails
        {
            ConfigId = configModel?.Id,
            ConfigVersion = ruleVersion,
            NormalizedSituation = situationKey,
            TotalPeople = peopleSummary.TotalPeople,
            InjuredPeopleCount = injuredPeople.Count,
            MedicalScore = medicalScore,
            MedicalIssueScore = medicalIssueScore,
            VictimSeverityScore = victimSeverityScore,
            MedicineUrgencyScore = medicineUrgencyScore,
            RequestTypeScore = requestTypeScore,
            ReliefScore = reliefScore,
            SupplyUrgencyScore = supplyUrgencyScore,
            WaterUrgencyScore = waterUrgencyScore,
            FoodUrgencyScore = foodUrgencyScore,
            BlanketUrgencyScore = blanketUrgencyScore,
            ClothingUrgencyScore = clothingUrgencyScore,
            VulnerabilityRaw = vulnerabilityRaw,
            VulnerabilityScore = vulnerabilityScore,
            SituationMultiplier = situationMultiplier,
            ReliefPressureMultiplier = reliefPressureMultiplier,
            MedicalSevereFlag = medicalSevere,
            CriticalSeverityFlag = criticalSeverity,
            UrgentMedicineFlag = urgentMedicine,
            SituationSevereFlag = situationSevere,
            DangerousSituationFlag = dangerousSituation,
            HasSevereFlag = hasSevereFlag,
            HasVulnerablePeople = hasVulnerablePeople,
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
            MedicalIssueBreakdown = medicalIssueBreakdown,
            ItemsNeeded = itemsNeeded,
            RawVariables = rawVariables,
            DerivedValues = derivedValues,
            ThresholdDecision = new SosPriorityThresholdDecision
            {
                PriorityScore = priorityScore,
                PriorityLevel = priorityLevel.ToString(),
                ThresholdPriorityLevel = thresholdPriorityLevel.ToString(),
                MedicalSevereFlag = medicalSevere,
                SituationSevereFlag = situationSevere,
                HasSevereFlag = hasSevereFlag,
                P1Threshold = config.PriorityLevel.P1Threshold,
                P2Threshold = config.PriorityLevel.P2Threshold,
                P3Threshold = config.PriorityLevel.P3Threshold
            },
            EscalationDecision = escalationDecision
        };

        var breakdownJson = JsonSerializer.Serialize(details);
        return Task.FromResult(new SosRuleEvaluationModel
        {
            SosRequestId = sosRequestId,
            ConfigId = configModel?.Id,
            ConfigVersion = ruleVersion,
            MedicalScore = medicalScore,
            InjuryScore = supplyUrgencyScore,
            MobilityScore = vulnerabilityScore,
            EnvironmentScore = situationMultiplier,
            FoodScore = reliefScore,
            TotalScore = priorityScore,
            PriorityLevel = priorityLevel,
            RuleVersion = ruleVersion,
            ItemsNeeded = itemsNeeded.Count > 0 ? JsonSerializer.Serialize(itemsNeeded) : null,
            BreakdownJson = breakdownJson,
            DetailsJson = breakdownJson,
            CreatedAt = DateTime.UtcNow
        });
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

    private static double ResolveVictimSeverityScore(SosPriorityRuleConfigDocument config, string severityKey)
    {
        if (string.IsNullOrWhiteSpace(severityKey))
        {
            return 0d;
        }

        return config.MedicalScore.VictimSeverityScore.TryGetValue(severityKey, out var configuredWeight)
            ? configuredWeight
            : config.MedicalScore.VictimSeverityScore.GetValueOrDefault("OTHER", 0d);
    }

    private static double ResolveMedicineUrgencyScore(SosPriorityRuleConfigDocument config, MedicineNeed? medicine)
    {
        if (medicine is null)
        {
            return 0d;
        }

        var rule = config.MedicalScore.MedicineUrgencyScore;
        var score = medicine.NeedsUrgentMedicine == true ? rule.NeedsUrgentMedicineScore : 0d;

        score += SumMappedScores(rule.ConditionScores, medicine.Conditions);
        score += SumMappedScores(rule.MedicalNeedScores, medicine.MedicalNeeds);

        return rule.MaxScore > 0 ? Math.Min(score, rule.MaxScore) : score;
    }

    private static double SumMappedScores(IReadOnlyDictionary<string, double> scores, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return 0d;
        }

        var total = 0d;
        foreach (var value in values.Select(SosPriorityRuleConfigSupport.NormalizeKey).Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            total += scores.TryGetValue(value, out var configuredScore)
                ? configuredScore
                : scores.GetValueOrDefault("OTHER", 0d);
        }

        return total;
    }

    private static bool HasUrgentMedicineNeed(MedicineNeed? medicine, double medicineUrgencyScore)
    {
        if (medicine is null)
        {
            return false;
        }

        return medicine.NeedsUrgentMedicine == true
            || medicineUrgencyScore >= 4d
            || HasAnyNormalized(medicine.MedicalNeeds, "FIRST_AID", "OXYGEN", "MEDICAL_DEVICE", "CHRONIC_MAINTENANCE");
    }

    private static double ResolveAgeWeight(SosPriorityRuleConfigDocument config, string? personType)
    {
        var normalizedPersonType = NormalizePersonType(personType);
        return config.MedicalScore.AgeWeights.TryGetValue(normalizedPersonType, out var ageWeight)
            ? ageWeight
            : config.MedicalScore.AgeWeights.GetValueOrDefault("ADULT", 1d);
    }

    private static double ResolveRequestTypeScore(SosPriorityRuleConfigDocument config, string? sosType)
    {
        var normalizedSosType = NormalizeRequestType(sosType);
        return config.RequestTypeScores.TryGetValue(normalizedSosType, out var score)
            ? score
            : config.RequestTypeScores.GetValueOrDefault("OTHER", 0d);
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

    private static double ResolveReliefPressureMultiplier(double reliefScore)
    {
        return 1d + Math.Min(0.25d, Math.Max(0d, reliefScore) / 80d);
    }

    private static PeopleSummary ResolvePeopleSummary(StructuredData? data)
    {
        var adult = Math.Max(0, data?.Incident?.PeopleCount?.Adult ?? data?.PeopleCount?.Adult ?? 0);
        var child = Math.Max(0, data?.Incident?.PeopleCount?.Child ?? data?.PeopleCount?.Child ?? 0);
        var elderly = Math.Max(0, data?.Incident?.PeopleCount?.Elderly ?? data?.PeopleCount?.Elderly ?? 0);
        var hasPregnantAny = (data?.Incident?.HasPregnantAny ?? false)
            || data?.PreparedProfiles?.Any(profile => profile.MedicalProfile?.SpecialSituation?.IsPregnant == true) == true;

        if (adult + child + elderly == 0 && data?.Victims is { Count: > 0 })
        {
            adult = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "ADULT");
            child = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "CHILD");
            elderly = data.Victims.Count(v => NormalizePersonType(v.PersonType) == "ELDERLY");
        }

        if (adult + child + elderly == 0 && data?.PreparedProfiles is { Count: > 0 })
        {
            adult = data.PreparedProfiles.Count(v => NormalizePersonType(v.PersonType) == "ADULT");
            child = data.PreparedProfiles.Count(v => NormalizePersonType(v.PersonType) == "CHILD");
            elderly = data.PreparedProfiles.Count(v => NormalizePersonType(v.PersonType) == "ELDERLY");
        }

        return new PeopleSummary(adult, child, elderly, hasPregnantAny);
    }

    private static List<InjuredPerson> BuildInjuredPeople(StructuredData? data)
    {
        if (data?.Victims is { Count: > 0 })
        {
            return data.Victims
                .Where(v => v.IncidentStatus?.IsInjured == true
                    || v.IncidentStatus?.MedicalIssues is { Count: > 0 }
                    || !string.IsNullOrWhiteSpace(v.IncidentStatus?.Severity))
                .Select(v => new InjuredPerson
                {
                    PersonType = v.PersonType,
                    Severity = v.IncidentStatus?.Severity,
                    MedicalIssues = (v.IncidentStatus?.MedicalIssues ?? [])
                        .Select(SosPriorityRuleConfigSupport.NormalizeKey)
                        .Where(issue => !string.IsNullOrWhiteSpace(issue))
                        .ToList()
                })
                .ToList();
        }

        if (data?.InjuredPersons is { Count: > 0 })
        {
            return data.InjuredPersons
                .Select(person => new InjuredPerson
                {
                    PersonType = person.PersonType,
                    Severity = person.Severity,
                    MedicalIssues = (person.MedicalIssues ?? [])
                        .Select(SosPriorityRuleConfigSupport.NormalizeKey)
                        .Where(issue => !string.IsNullOrWhiteSpace(issue))
                        .ToList()
                })
                .ToList();
        }

        if (data?.MedicalIssues is { Count: > 0 })
        {
            return
            [
                new InjuredPerson
                {
                    PersonType = "ADULT",
                    MedicalIssues = data.MedicalIssues
                        .Select(SosPriorityRuleConfigSupport.NormalizeKey)
                        .Where(issue => !string.IsNullOrWhiteSpace(issue))
                        .ToList()
                }
            ];
        }

        return [];
    }

    private static int? ResolveBlanketRequestCount(
        StructuredData? data,
        SosPriorityRuleConfigDocument config,
        int totalPeople,
        bool blanketsSelected)
    {
        var requestCount = data?.GroupNeeds?.Blanket?.RequestCount
            ?? data?.SupplyDetails?.BlanketRequestCount;
        if (requestCount.HasValue)
        {
            return requestCount.Value;
        }

        var areBlanketsEnough = ResolveAreBlanketsEnough(data);
        if (!blanketsSelected || areBlanketsEnough != false)
        {
            return null;
        }

        var blanketAvailability = NormalizeBlanketAvailability(data?.GroupNeeds?.Blanket?.Availability);
        if (totalPeople > 0 && blanketAvailability is "NONE" or "NOT_ENOUGH")
        {
            return totalPeople;
        }

        return config.UiConstraints.BlanketRequestCountDefault;
    }

    private static bool? ResolveAreBlanketsEnough(StructuredData? data)
    {
        var explicitValue = data?.GroupNeeds?.Blanket?.AreBlanketsEnough
            ?? data?.SupplyDetails?.AreBlanketsEnough;
        if (explicitValue.HasValue)
        {
            return explicitValue.Value;
        }

        return NormalizeBlanketAvailability(data?.GroupNeeds?.Blanket?.Availability) switch
        {
            "NONE" or "NOT_ENOUGH" or "PARTIALLY_LACKING" => false,
            "ENOUGH" => true,
            _ => null
        };
    }

    private static string NormalizeBlanketAvailability(string? availability)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(availability) switch
        {
            "NOTENOUGH" => "NOT_ENOUGH",
            "NOT_ENOUGH" => "NOT_ENOUGH",
            "NO" or "NONE" => "NONE",
            "ENOUGH" or "YES" => "ENOUGH",
            "PARTIAL" or "PARTIALLY_LACKING" => "PARTIALLY_LACKING",
            var normalized => normalized
        };
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

        if (data?.SupplyDetails?.ClothingPersons is { Count: > 0 })
        {
            return data.SupplyDetails.ClothingPersons.Count;
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
            || HasSupply(data?.Supplies, "BLANKET")
            || data?.GroupNeeds?.Blanket is not null
            || data?.SupplyDetails?.BlanketRequestCount is > 0
            || data?.SupplyDetails?.AreBlanketsEnough is not null;
    }

    private static bool IsClothingSelected(StructuredData? data)
    {
        return HasSupply(data?.GroupNeeds?.Supplies, "CLOTHING")
            || HasSupply(data?.Supplies, "CLOTHING")
            || data?.GroupNeeds?.Clothing is not null
            || data?.SupplyDetails?.ClothingPersons is { Count: > 0 }
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
            .Any(supply => supply == normalizedExpectedSupply
                || supply == normalizedExpectedSupply + "S"
                || (normalizedExpectedSupply == "CLOTHING" && supply == "CLOTHES"));
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

    private static string NormalizeRequestType(string? sosType)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(sosType) switch
        {
            "SUPPLY" or "RELIEF" => "RELIEF",
            "RESCUE" or "MEDICAL" or "EVACUATION" => "RESCUE",
            "BOTH" or "MIXED" or "RESCUE_RELIEF" or "RESCUE_AND_RELIEF" => "BOTH",
            var normalized when string.IsNullOrWhiteSpace(normalized) => "OTHER",
            _ => "OTHER"
        };
    }

    private static string NormalizeVictimSeverity(string? severity)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(severity) switch
        {
            "LIFE_THREATENING" or "EMERGENCY" => "CRITICAL",
            "SERIOUS" => "SEVERE",
            "MODERATE" => "MEDIUM",
            "MINOR" => "LOW",
            var normalized => normalized
        };
    }

    private static bool IsDangerousSituation(string? situation)
    {
        return SosPriorityRuleConfigSupport.NormalizeKey(situation) is
            "TRAPPED" or
            "FLOODING" or
            "COLLAPSED" or
            "DANGER_ZONE" or
            "CANNOT_MOVE";
    }

    private static (SosPriorityLevel PriorityLevel, SosPriorityEscalationDecision Decision) ResolvePriorityEscalation(
        SosPriorityLevel thresholdPriorityLevel,
        bool criticalSeverity,
        bool medicalSevere,
        bool dangerousSituation,
        bool hasVulnerablePeople,
        bool urgentMedicine,
        double supplyUrgencyScore,
        double reliefScore,
        bool? areBlanketsEnough)
    {
        var finalPriority = thresholdPriorityLevel;
        var reasons = new List<string>();

        void EscalateTo(SosPriorityLevel minimumPriority, string reason)
        {
            reasons.Add(reason);
            if (PriorityRank(minimumPriority) > PriorityRank(finalPriority))
            {
                finalPriority = minimumPriority;
            }
        }

        var hasReliefPressure = supplyUrgencyScore >= 14d
            || reliefScore >= 18d
            || urgentMedicine
            || areBlanketsEnough == false;

        if (criticalSeverity)
        {
            EscalateTo(SosPriorityLevel.High, "victim_severity_CRITICAL_min_HIGH");
        }

        if (medicalSevere && dangerousSituation && hasVulnerablePeople)
        {
            EscalateTo(SosPriorityLevel.High, "severe_medical_dangerous_situation_vulnerable_min_HIGH");
        }

        if (criticalSeverity && (dangerousSituation || urgentMedicine || hasVulnerablePeople))
        {
            EscalateTo(SosPriorityLevel.Critical, "critical_severity_with_danger_or_urgent_or_vulnerable_min_CRITICAL");
        }

        if (medicalSevere && dangerousSituation && hasVulnerablePeople && hasReliefPressure)
        {
            EscalateTo(SosPriorityLevel.Critical, "severe_medical_dangerous_vulnerable_relief_pressure_min_CRITICAL");
        }

        return (finalPriority, new SosPriorityEscalationDecision
        {
            Applied = finalPriority != thresholdPriorityLevel,
            OriginalPriorityLevel = thresholdPriorityLevel.ToString(),
            FinalPriorityLevel = finalPriority.ToString(),
            MinimumPriorityLevel = finalPriority != thresholdPriorityLevel ? finalPriority.ToString() : null,
            Reasons = reasons
        });
    }

    private static int PriorityRank(SosPriorityLevel priorityLevel)
    {
        return priorityLevel switch
        {
            SosPriorityLevel.Critical => 4,
            SosPriorityLevel.High => 3,
            SosPriorityLevel.Medium => 2,
            _ => 1
        };
    }

    private static bool HasAnyNormalized(IEnumerable<string>? values, params string[] expectedValues)
    {
        if (values is null)
        {
            return false;
        }

        var expected = expectedValues
            .Select(SosPriorityRuleConfigSupport.NormalizeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return values
            .Select(SosPriorityRuleConfigSupport.NormalizeKey)
            .Any(expected.Contains);
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

    private static List<string> DetermineItemsNeeded(StructuredData? data, string? sosType, bool blanketsSelected, bool clothingSelected)
    {
        // SupplyNeed mobile enum: WATER | FOOD | CLOTHES | BLANKET | MEDICINE | OTHER
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var peopleSummary = ResolvePeopleSummary(data);
        var situation = NormalizeSituation(data?.Incident?.Situation ?? data?.Situation);
        var injuredPeople = BuildInjuredPeople(data);

        if (injuredPeople.Count > 0 || data?.Incident?.HasInjured == true || data?.HasInjured == true)
        {
            items.Add("MEDICINE");
        }

        if (situation is "FLOODING" or "TRAPPED" or "COLLAPSED" or "DANGER_ZONE")
        {
            items.Add("OTHER");
        }

        if (peopleSummary.TotalPeople > 0)
        {
            items.Add("WATER");
            items.Add("FOOD");
        }

        if (blanketsSelected)
        {
            items.Add("BLANKET");
        }

        if (clothingSelected)
        {
            items.Add("CLOTHES");
        }

        // Merge supplies already declared in group_needs.supplies
        var declaredSupplies = data?.GroupNeeds?.Supplies ?? data?.Supplies;
        if (declaredSupplies is { Count: > 0 })
        {
            foreach (var s in declaredSupplies)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    items.Add(s.ToUpperInvariant());
            }
        }

        return items.OrderBy(item => item).ToList();
    }

    private sealed class StructuredData
    {
        public string? Situation { get; set; }
        public bool? HasInjured { get; set; }
        public PeopleCount? PeopleCount { get; set; }
        public List<string>? MedicalIssues { get; set; }
        public List<string>? Supplies { get; set; }
        public List<InjuredPerson>? InjuredPersons { get; set; }
        public SupplyDetails? SupplyDetails { get; set; }
        public Incident? Incident { get; set; }
        public GroupNeeds? GroupNeeds { get; set; }
        public List<Victim>? Victims { get; set; }
        public List<PreparedProfile>? PreparedProfiles { get; set; }
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
        public MedicineNeed? Medicine { get; set; }
        public ClothingNeed? Clothing { get; set; }
    }

    private sealed class SupplyDetails
    {
        public string? WaterDuration { get; set; }
        public string? FoodDuration { get; set; }
        public bool? AreBlanketsEnough { get; set; }
        public int? BlanketRequestCount { get; set; }
        public string? ClothingStatus { get; set; }
        public List<LegacyClothingPerson>? ClothingPersons { get; set; }
    }

    private sealed class LegacyClothingPerson
    {
        public string? PersonType { get; set; }
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
        public bool? IsColdOrWet { get; set; }
        public bool? AreBlanketsEnough { get; set; }
        public string? Availability { get; set; }
        public int? RequestCount { get; set; }
    }

    private sealed class MedicineNeed
    {
        public bool? NeedsUrgentMedicine { get; set; }
        public List<string>? Conditions { get; set; }
        public List<string>? MedicalNeeds { get; set; }
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
        public string? Severity { get; set; }
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

    private sealed class PreparedProfile
    {
        public string? PersonType { get; set; }
        public PreparedMedicalProfile? MedicalProfile { get; set; }
    }

    private sealed class PreparedMedicalProfile
    {
        public PreparedSpecialSituation? SpecialSituation { get; set; }
    }

    private sealed class PreparedSpecialSituation
    {
        public bool? IsPregnant { get; set; }
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
        public string? Severity { get; set; }
        public List<string> MedicalIssues { get; set; } = [];
    }

    private readonly record struct PeopleSummary(int AdultCount, int ChildCount, int ElderlyCount, bool HasPregnantAny)
    {
        public int TotalPeople => AdultCount + ChildCount + ElderlyCount;
    }
}
