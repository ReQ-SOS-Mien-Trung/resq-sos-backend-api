using System.Text.Json.Serialization;

namespace RESQ.Domain.Entities.Emergency;

public class SosPriorityEvaluationDetails
{
    [JsonPropertyName("normalized_situation")]
    public string? NormalizedSituation { get; set; }

    [JsonPropertyName("total_people")]
    public int TotalPeople { get; set; }

    [JsonPropertyName("injured_people_count")]
    public int InjuredPeopleCount { get; set; }

    [JsonPropertyName("medical_score")]
    public double MedicalScore { get; set; }

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

    [JsonPropertyName("medical_severe_flag")]
    public bool MedicalSevereFlag { get; set; }

    [JsonPropertyName("situation_severe_flag")]
    public bool SituationSevereFlag { get; set; }

    [JsonPropertyName("has_severe_flag")]
    public bool HasSevereFlag { get; set; }

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

    [JsonPropertyName("total")]
    public double Total { get; set; }
}