using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("rescuer_profiles")]
public class RescuerProfile
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("rescuer_type")]
    [StringLength(50)]
    public string? RescuerType { get; set; }

    [Column("is_eligible_rescuer")]
    public bool IsEligibleRescuer { get; set; } = false;

    [Column("step")]
    public int Step { get; set; } = 0;

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("approved_at", TypeName = "timestamp with time zone")]
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("ApprovedBy")]
    public virtual User? ApprovedByUser { get; set; }

    [InverseProperty("RescuerProfile")]
    public virtual ICollection<MissionTeamMemberEvaluation> MissionTeamMemberEvaluations { get; set; } = new List<MissionTeamMemberEvaluation>();

    [InverseProperty("RescuerProfile")]
    public virtual RescuerScore? RescuerScore { get; set; }
}
