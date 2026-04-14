using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("rescuer_score_visibility_configs")]
public class RescuerScoreVisibilityConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("minimum_evaluation_count")]
    public int MinimumEvaluationCount { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}
