using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("sos_priority_rule_configs")]
public class SosPriorityRuleConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("config_version")]
    [StringLength(100)]
    public string ConfigVersion { get; set; } = "SOS_PRIORITY_V2";

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("activated_at", TypeName = "timestamp with time zone")]
    public DateTime? ActivatedAt { get; set; }

    [Column("activated_by")]
    public Guid? ActivatedBy { get; set; }

    [Column("config_json", TypeName = "jsonb")]
    public string ConfigJson { get; set; } = "{}";

    [Column("issue_weights_json", TypeName = "jsonb")]
    public string IssueWeightsJson { get; set; } = "{}";

    [Column("medical_severe_issues_json", TypeName = "jsonb")]
    public string MedicalSevereIssuesJson { get; set; } = "[]";

    [Column("age_weights_json", TypeName = "jsonb")]
    public string AgeWeightsJson { get; set; } = "{}";

    [Column("request_type_scores_json", TypeName = "jsonb")]
    public string RequestTypeScoresJson { get; set; } = "{}";

    [Column("situation_multipliers_json", TypeName = "jsonb")]
    public string SituationMultipliersJson { get; set; } = "[]";

    [Column("priority_thresholds_json", TypeName = "jsonb")]
    public string PriorityThresholdsJson { get; set; } = "{}";

    [Column("water_urgency_scores_json", TypeName = "jsonb")]
    public string WaterUrgencyScoresJson { get; set; } = "{}";

    [Column("food_urgency_scores_json", TypeName = "jsonb")]
    public string FoodUrgencyScoresJson { get; set; } = "{}";

    [Column("blanket_urgency_rules_json", TypeName = "jsonb")]
    public string BlanketUrgencyRulesJson { get; set; } = "{}";

    [Column("clothing_urgency_rules_json", TypeName = "jsonb")]
    public string ClothingUrgencyRulesJson { get; set; } = "{}";

    [Column("vulnerability_rules_json", TypeName = "jsonb")]
    public string VulnerabilityRulesJson { get; set; } = "{}";

    [Column("vulnerability_score_expression_json", TypeName = "jsonb")]
    public string VulnerabilityScoreExpressionJson { get; set; } = "{}";

    [Column("relief_score_expression_json", TypeName = "jsonb")]
    public string ReliefScoreExpressionJson { get; set; } = "{}";

    [Column("priority_score_expression_json", TypeName = "jsonb")]
    public string PriorityScoreExpressionJson { get; set; } = "{}";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
