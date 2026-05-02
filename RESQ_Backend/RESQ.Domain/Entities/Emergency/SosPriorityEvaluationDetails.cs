using System.Text.Json.Serialization;

namespace RESQ.Domain.Entities.Emergency;

public class SosPriorityEvaluationDetails
{
    [JsonPropertyName("config_id")]
    public int? ConfigId { get; set; }

    [JsonPropertyName("config_version")]
    public string? ConfigVersion { get; set; }

    [JsonPropertyName("normalized_situation")]
    public string? NormalizedSituation { get; set; }

    [JsonPropertyName("total_people")]
    public int TotalPeople { get; set; }

    [JsonPropertyName("injured_people_count")]
    public int InjuredPeopleCount { get; set; }

    [JsonPropertyName("medical_score")]
    public double MedicalScore { get; set; }

    [JsonPropertyName("medical_issue_score")]
    public double MedicalIssueScore { get; set; }

    [JsonPropertyName("victim_severity_score")]
    public double VictimSeverityScore { get; set; }

    [JsonPropertyName("medicine_urgency_score")]
    public double MedicineUrgencyScore { get; set; }

    [JsonPropertyName("request_type_score")]
    public double RequestTypeScore { get; set; }

    [JsonPropertyName("relief_score")]
    public double ReliefScore { get; set; }

    [JsonPropertyName("supply_urgency_score")]
    public double SupplyUrgencyScore { get; set; }

    [JsonPropertyName("water_urgency_score")]
    public int WaterUrgencyScore { get; set; }

    [JsonPropertyName("food_urgency_score")]
    public int FoodUrgencyScore { get; set; }

    [JsonPropertyName("blanket_urgency_score")]
    public int BlanketUrgencyScore { get; set; }

    [JsonPropertyName("clothing_urgency_score")]
    public int ClothingUrgencyScore { get; set; }

    [JsonPropertyName("vulnerability_raw")]
    public double VulnerabilityRaw { get; set; }

    [JsonPropertyName("vulnerability_score")]
    public double VulnerabilityScore { get; set; }

    [JsonPropertyName("situation_multiplier")]
    public double SituationMultiplier { get; set; }

    [JsonPropertyName("relief_pressure_multiplier")]
    public double ReliefPressureMultiplier { get; set; }

    [JsonPropertyName("medical_severe_flag")]
    public bool MedicalSevereFlag { get; set; }

    [JsonPropertyName("critical_severity_flag")]
    public bool CriticalSeverityFlag { get; set; }

    [JsonPropertyName("urgent_medicine_flag")]
    public bool UrgentMedicineFlag { get; set; }

    [JsonPropertyName("situation_severe_flag")]
    public bool SituationSevereFlag { get; set; }

    [JsonPropertyName("dangerous_situation_flag")]
    public bool DangerousSituationFlag { get; set; }

    [JsonPropertyName("has_severe_flag")]
    public bool HasSevereFlag { get; set; }

    [JsonPropertyName("has_vulnerable_people")]
    public bool HasVulnerablePeople { get; set; }

    [JsonPropertyName("water_duration")]
    public string? WaterDuration { get; set; }

    [JsonPropertyName("food_duration")]
    public string? FoodDuration { get; set; }

    [JsonPropertyName("blankets_selected")]
    public bool BlanketsSelected { get; set; }

    [JsonPropertyName("are_blankets_enough")]
    public bool? AreBlanketsEnough { get; set; }

    [JsonPropertyName("blanket_request_count")]
    public int? BlanketRequestCount { get; set; }

    [JsonPropertyName("clothing_selected")]
    public bool ClothingSelected { get; set; }

    [JsonPropertyName("clothing_needed_people_count")]
    public int? ClothingNeededPeopleCount { get; set; }

    [JsonPropertyName("children_count")]
    public int ChildrenCount { get; set; }

    [JsonPropertyName("elderly_count")]
    public int ElderlyCount { get; set; }

    [JsonPropertyName("has_pregnant_any")]
    public bool HasPregnantAny { get; set; }

    [JsonPropertyName("medical_issue_breakdown")]
    public List<SosMedicalIssueBreakdownItem> MedicalIssueBreakdown { get; set; } = [];

    [JsonPropertyName("items_needed")]
    public List<string> ItemsNeeded { get; set; } = [];

    [JsonPropertyName("raw_variables")]
    public Dictionary<string, double> RawVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("derived_values")]
    public Dictionary<string, double> DerivedValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("threshold_decision")]
    public SosPriorityThresholdDecision? ThresholdDecision { get; set; }

    [JsonPropertyName("escalation_decision")]
    public SosPriorityEscalationDecision? EscalationDecision { get; set; }
}

public class SosMedicalIssueBreakdownItem
{
    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("issue_scores")]
    public Dictionary<string, double> IssueScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("issue_weight_sum")]
    public double IssueWeightSum { get; set; }

    [JsonPropertyName("age_weight")]
    public double AgeWeight { get; set; }

    [JsonPropertyName("victim_severity")]
    public string? VictimSeverity { get; set; }

    [JsonPropertyName("victim_severity_score")]
    public double VictimSeverityScore { get; set; }

    [JsonPropertyName("total")]
    public double Total { get; set; }
}

public class SosPriorityThresholdDecision
{
    [JsonPropertyName("priority_score")]
    public double PriorityScore { get; set; }

    [JsonPropertyName("priority_level")]
    public string PriorityLevel { get; set; } = string.Empty;

    [JsonPropertyName("threshold_priority_level")]
    public string ThresholdPriorityLevel { get; set; } = string.Empty;

    [JsonPropertyName("medical_severe_flag")]
    public bool MedicalSevereFlag { get; set; }

    [JsonPropertyName("situation_severe_flag")]
    public bool SituationSevereFlag { get; set; }

    [JsonPropertyName("has_severe_flag")]
    public bool HasSevereFlag { get; set; }

    [JsonPropertyName("p1_threshold")]
    public int P1Threshold { get; set; }

    [JsonPropertyName("p2_threshold")]
    public int P2Threshold { get; set; }

    [JsonPropertyName("p3_threshold")]
    public int P3Threshold { get; set; }
}

public class SosPriorityEscalationDecision
{
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("original_priority_level")]
    public string OriginalPriorityLevel { get; set; } = string.Empty;

    [JsonPropertyName("final_priority_level")]
    public string FinalPriorityLevel { get; set; } = string.Empty;

    [JsonPropertyName("minimum_priority_level")]
    public string? MinimumPriorityLevel { get; set; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = [];
}
