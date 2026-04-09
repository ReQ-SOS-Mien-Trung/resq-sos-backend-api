using System.Text.Json.Serialization;

namespace RESQ.Domain.Entities.System;

public class SosPriorityRuleConfigDocument
{
    [JsonPropertyName("config_version")]
    public string ConfigVersion { get; set; } = "SOS_PRIORITY_V2";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("medical_severe_issues")]
    public List<string> MedicalSevereIssues { get; set; } =
    [
        "UNCONSCIOUS",
        "BREATHING_DIFFICULTY",
        "CHEST_PAIN_STROKE",
        "DROWNING",
        "SEVERELY_BLEEDING"
    ];

    [JsonPropertyName("request_type_scores")]
    public Dictionary<string, double> RequestTypeScores { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RESCUE"] = 30,
        ["RELIEF"] = 20,
        ["OTHER"] = 10
    };

    [JsonPropertyName("priority_score")]
    public SosPriorityScoreConfig PriorityScore { get; set; } = new();

    [JsonPropertyName("medical_score")]
    public SosMedicalScoreConfig MedicalScore { get; set; } = new();

    [JsonPropertyName("relief_score")]
    public SosReliefScoreConfig ReliefScore { get; set; } = new();

    [JsonPropertyName("situation_multiplier")]
    public Dictionary<string, double> SituationMultiplier { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FLOODING"] = 1.5,
        ["COLLAPSED"] = 1.5,
        ["TRAPPED"] = 1.3,
        ["DANGER_ZONE"] = 1.3,
        ["CANNOT_MOVE"] = 1.2,
        ["OTHER"] = 1.0,
        ["DEFAULT_WHEN_NULL"] = 1.0
    };

    [JsonPropertyName("priority_level")]
    public SosPriorityLevelConfig PriorityLevel { get; set; } = new();

    [JsonPropertyName("ui_constraints")]
    public SosUiConstraintsConfig UiConstraints { get; set; } = new();

    [JsonPropertyName("ui_options")]
    public SosUiOptionsConfig UiOptions { get; set; } = new();

    [JsonPropertyName("display_labels")]
    public SosDisplayLabelsConfig DisplayLabels { get; set; } = new();
}

public class SosPriorityScoreConfig
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "ROUND((medical_score + relief_score) * situation_multiplier)";

    [JsonPropertyName("use_request_type_score")]
    public bool UseRequestTypeScore { get; set; } = false;

    [JsonPropertyName("expression")]
    public SosExpressionNode Expression { get; set; } = SosExpressionNode.Unary(
        "ROUND",
        SosExpressionNode.Binary(
            "MUL",
            SosExpressionNode.Binary(
                "ADD",
                SosExpressionNode.VarRef("medical_score"),
                SosExpressionNode.VarRef("relief_score")),
            SosExpressionNode.VarRef("situation_multiplier")));
}

public class SosMedicalScoreConfig
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "SUM(issue_weight_sum_per_injured_person * age_weight)";

    [JsonPropertyName("age_weights")]
    public Dictionary<string, double> AgeWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADULT"] = 1.0,
        ["CHILD"] = 1.4,
        ["ELDERLY"] = 1.3
    };

    [JsonPropertyName("medical_issue_severity")]
    public Dictionary<string, double> MedicalIssueSeverity { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNCONSCIOUS"] = 5,
        ["BREATHING_DIFFICULTY"] = 5,
        ["CHEST_PAIN_STROKE"] = 5,
        ["DROWNING"] = 5,
        ["SEVERELY_BLEEDING"] = 4,
        ["BLEEDING"] = 4,
        ["BURNS"] = 4,
        ["HEAD_INJURY"] = 4,
        ["CANNOT_MOVE"] = 4,
        ["HIGH_FEVER"] = 3,
        ["DEHYDRATION"] = 3,
        ["FRACTURE"] = 3,
        ["INFANT_NEEDS_MILK"] = 3,
        ["LOST_PARENT"] = 3,
        ["CHRONIC_DISEASE"] = 2,
        ["CONFUSION"] = 2,
        ["NEEDS_MEDICAL_DEVICE"] = 2,
        ["OTHER"] = 1
    };
}

public class SosReliefScoreConfig
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "supply_urgency_score + vulnerability_score";

    [JsonPropertyName("expression")]
    public SosExpressionNode Expression { get; set; } = SosExpressionNode.Binary(
        "ADD",
        SosExpressionNode.VarRef("supply_urgency_score"),
        SosExpressionNode.VarRef("vulnerability_score"));

    [JsonPropertyName("supply_urgency_score")]
    public SosSupplyUrgencyScoreConfig SupplyUrgencyScore { get; set; } = new();

    [JsonPropertyName("vulnerability_score")]
    public SosVulnerabilityScoreConfig VulnerabilityScore { get; set; } = new();
}

public class SosSupplyUrgencyScoreConfig
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "water_urgency_score + food_urgency_score + blanket_urgency_score + clothing_urgency_score";

    [JsonPropertyName("water_urgency_score")]
    public Dictionary<string, int> WaterUrgencyScore { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNDER_6H"] = 10,
        ["6_TO_12H"] = 7,
        ["12_TO_24H"] = 4,
        ["1_TO_2_DAYS"] = 2,
        ["OVER_2_DAYS"] = 0,
        ["NOT_SELECTED"] = 0
    };

    [JsonPropertyName("food_urgency_score")]
    public Dictionary<string, int> FoodUrgencyScore { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNDER_12H"] = 7,
        ["12_TO_24H"] = 5,
        ["1_TO_2_DAYS"] = 3,
        ["2_TO_3_DAYS"] = 1,
        ["OVER_3_DAYS"] = 0,
        ["NOT_SELECTED"] = 0
    };

    [JsonPropertyName("blanket_urgency_score")]
    public SosBlanketUrgencyScoreConfig BlanketUrgencyScore { get; set; } = new();

    [JsonPropertyName("clothing_urgency_score")]
    public SosClothingUrgencyScoreConfig ClothingUrgencyScore { get; set; } = new();
}

public class SosBlanketUrgencyScoreConfig
{
    [JsonPropertyName("apply_only_when_supply_selected")]
    public bool ApplyOnlyWhenSupplySelected { get; set; } = true;

    [JsonPropertyName("apply_only_when_are_blankets_enough_is_false")]
    public bool ApplyOnlyWhenAreBlanketsEnoughIsFalse { get; set; } = true;

    [JsonPropertyName("none_or_not_selected_score")]
    public int NoneOrNotSelectedScore { get; set; } = 0;

    [JsonPropertyName("requested_count_equals_1_score")]
    public int RequestedCountEquals1Score { get; set; } = 1;

    [JsonPropertyName("requested_count_more_than_half_people_score")]
    public int RequestedCountMoreThanHalfPeopleScore { get; set; } = 3;

    [JsonPropertyName("requested_count_between_2_and_half_people_score")]
    public int RequestedCountBetween2AndHalfPeopleScore { get; set; } = 2;

    [JsonPropertyName("half_people_operator")]
    public string HalfPeopleOperator { get; set; } = ">";
}

public class SosClothingUrgencyScoreConfig
{
    [JsonPropertyName("apply_only_when_supply_selected")]
    public bool ApplyOnlyWhenSupplySelected { get; set; } = true;

    [JsonPropertyName("none_or_not_selected_score")]
    public int NoneOrNotSelectedScore { get; set; } = 0;

    [JsonPropertyName("needed_people_equals_1_score")]
    public int NeededPeopleEquals1Score { get; set; } = 1;

    [JsonPropertyName("needed_people_more_than_half_people_score")]
    public int NeededPeopleMoreThanHalfPeopleScore { get; set; } = 3;

    [JsonPropertyName("needed_people_between_2_and_half_people_score")]
    public int NeededPeopleBetween2AndHalfPeopleScore { get; set; } = 2;

    [JsonPropertyName("half_people_operator")]
    public string HalfPeopleOperator { get; set; } = ">";
}

public class SosVulnerabilityScoreConfig
{
    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "MIN(vulnerability_raw, supply_urgency_score * cap_ratio)";

    [JsonPropertyName("expression")]
    public SosExpressionNode Expression { get; set; } = SosExpressionNode.Binary(
        "MIN",
        SosExpressionNode.VarRef("vulnerability_raw"),
        SosExpressionNode.Binary(
            "MUL",
            SosExpressionNode.VarRef("supply_urgency_score"),
            SosExpressionNode.VarRef("cap_ratio")));

    [JsonPropertyName("vulnerability_raw")]
    public SosVulnerabilityRawConfig VulnerabilityRaw { get; set; } = new();

    [JsonPropertyName("cap_ratio")]
    public double CapRatio { get; set; } = 0.10;
}

public class SosVulnerabilityRawConfig
{
    [JsonPropertyName("CHILD_PER_PERSON")]
    public double ChildPerPerson { get; set; } = 1;

    [JsonPropertyName("ELDERLY_PER_PERSON")]
    public double ElderlyPerPerson { get; set; } = 1;

    [JsonPropertyName("HAS_PREGNANT_ANY")]
    public double HasPregnantAny { get; set; } = 2;
}

public class SosPriorityLevelConfig
{
    [JsonPropertyName("P1_THRESHOLD")]
    public int P1Threshold { get; set; } = 70;

    [JsonPropertyName("P2_THRESHOLD")]
    public int P2Threshold { get; set; } = 45;

    [JsonPropertyName("P3_THRESHOLD")]
    public int P3Threshold { get; set; } = 25;

    [JsonPropertyName("rule")]
    public string Rule { get; set; } = "P1/P2 require has_severe_flag, P3 only threshold, else P4";
}

public class SosUiConstraintsConfig
{
    [JsonPropertyName("MIN_TOTAL_PEOPLE_TO_PROCEED")]
    public int MinTotalPeopleToProceed { get; set; } = 1;

    [JsonPropertyName("BLANKET_REQUEST_COUNT_DEFAULT")]
    public int BlanketRequestCountDefault { get; set; } = 1;

    [JsonPropertyName("BLANKET_REQUEST_COUNT_MIN")]
    public int BlanketRequestCountMin { get; set; } = 1;

    [JsonPropertyName("BLANKET_REQUEST_COUNT_MAX_FORMULA")]
    public string BlanketRequestCountMaxFormula { get; set; } = "max(1, people_count)";
}

public class SosUiOptionsConfig
{
    [JsonPropertyName("WATER_DURATION")]
    public List<string> WaterDuration { get; set; } =
    [
        "UNDER_6H",
        "6_TO_12H",
        "12_TO_24H",
        "1_TO_2_DAYS",
        "OVER_2_DAYS"
    ];

    [JsonPropertyName("FOOD_DURATION")]
    public List<string> FoodDuration { get; set; } =
    [
        "UNDER_12H",
        "12_TO_24H",
        "1_TO_2_DAYS",
        "2_TO_3_DAYS",
        "OVER_3_DAYS"
    ];
}

public class SosDisplayLabelsConfig
{
    [JsonPropertyName("medical_issues")]
    public Dictionary<string, string> MedicalIssues { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNCONSCIOUS"] = "Bất tỉnh",
        ["BREATHING_DIFFICULTY"] = "Khó thở",
        ["CHEST_PAIN_STROKE"] = "Đau ngực/đột quỵ",
        ["DROWNING"] = "Đuối nước",
        ["SEVERELY_BLEEDING"] = "Chảy máu nặng",
        ["BLEEDING"] = "Chảy máu",
        ["BURNS"] = "Bỏng",
        ["HEAD_INJURY"] = "Chấn thương đầu",
        ["CANNOT_MOVE"] = "Không thể di chuyển",
        ["HIGH_FEVER"] = "Sốt cao",
        ["DEHYDRATION"] = "Mất nước",
        ["FRACTURE"] = "Gãy xương",
        ["INFANT_NEEDS_MILK"] = "Trẻ sơ sinh cần sữa",
        ["LOST_PARENT"] = "Trẻ lạc người thân",
        ["CHRONIC_DISEASE"] = "Bệnh nền",
        ["CONFUSION"] = "Mất phương hướng",
        ["NEEDS_MEDICAL_DEVICE"] = "Cần thiết bị y tế",
        ["OTHER"] = "Khác",
        ["PREGNANCY"] = "Bầu",
        ["COVID"] = "Covid"
    };

    [JsonPropertyName("situations")]
    public Dictionary<string, string> Situations { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FLOODING"] = "Ngập lụt",
        ["COLLAPSED"] = "Sập công trình",
        ["TRAPPED"] = "Mắc kẹt",
        ["DANGER_ZONE"] = "Vùng nguy hiểm",
        ["CANNOT_MOVE"] = "Không thể di chuyển",
        ["OTHER"] = "Khác",
        ["DEFAULT_WHEN_NULL"] = "Mặc định"
    };

    [JsonPropertyName("water_duration")]
    public Dictionary<string, string> WaterDuration { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNDER_6H"] = "Dưới 6 giờ",
        ["6_TO_12H"] = "6 đến 12 giờ",
        ["12_TO_24H"] = "12 đến 24 giờ",
        ["1_TO_2_DAYS"] = "1 đến 2 ngày",
        ["OVER_2_DAYS"] = "Trên 2 ngày",
        ["NOT_SELECTED"] = "Chưa chọn"
    };

    [JsonPropertyName("food_duration")]
    public Dictionary<string, string> FoodDuration { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNDER_12H"] = "Dưới 12 giờ",
        ["12_TO_24H"] = "12 đến 24 giờ",
        ["1_TO_2_DAYS"] = "1 đến 2 ngày",
        ["2_TO_3_DAYS"] = "2 đến 3 ngày",
        ["OVER_3_DAYS"] = "Trên 3 ngày",
        ["NOT_SELECTED"] = "Chưa chọn"
    };

    [JsonPropertyName("age_groups")]
    public Dictionary<string, string> AgeGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ADULT"] = "Người lớn",
        ["CHILD"] = "Trẻ em",
        ["ELDERLY"] = "Người cao tuổi"
    };

    [JsonPropertyName("request_types")]
    public Dictionary<string, string> RequestTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RESCUE"] = "Cứu nạn",
        ["RELIEF"] = "Tiếp tế",
        ["OTHER"] = "Khác"
    };
}
