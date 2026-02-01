using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("sos_rule_evaluations")]
public partial class SosRuleEvaluation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sos_request_id")]
    public int? SosRequestId { get; set; }

    [Column("medical_score")]
    public double? MedicalScore { get; set; }

    [Column("food_score")]
    public double? FoodScore { get; set; }

    [Column("injury_score")]
    public double? InjuryScore { get; set; }

    [Column("mobility_score")]
    public double? MobilityScore { get; set; }

    [Column("environment_score")]
    public double? EnvironmentScore { get; set; }

    [Column("total_score")]
    public double? TotalScore { get; set; }

    [Column("priority_level")]
    [StringLength(10)]
    public string? PriorityLevel { get; set; }

    [Column("rule_version")]
    [StringLength(50)]
    public string? RuleVersion { get; set; }

    [Column("items_needed", TypeName = "jsonb")]
    public string? ItemsNeeded { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("SosRequestId")]
    [InverseProperty("SosRuleEvaluations")]
    public virtual SosRequest? SosRequest { get; set; }
}
