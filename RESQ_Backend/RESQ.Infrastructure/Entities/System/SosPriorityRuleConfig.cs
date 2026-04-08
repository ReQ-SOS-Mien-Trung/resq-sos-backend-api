using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("sos_priority_rule_configs")]
public class SosPriorityRuleConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

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

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
