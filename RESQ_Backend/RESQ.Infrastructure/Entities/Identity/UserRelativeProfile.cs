using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("user_relative_profiles")]
public class UserRelativeProfile
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("display_name")]
    [StringLength(150)]
    public string DisplayName { get; set; } = null!;

    [Column("phone_number")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("person_type")]
    [StringLength(20)]
    public string PersonType { get; set; } = null!;

    [Column("relation_group")]
    [StringLength(20)]
    public string RelationGroup { get; set; } = null!;

    [Column("tags_json", TypeName = "jsonb")]
    public string TagsJson { get; set; } = "[]";

    [Column("medical_baseline_note")]
    public string? MedicalBaselineNote { get; set; }

    [Column("special_needs_note")]
    public string? SpecialNeedsNote { get; set; }

    [Column("special_diet_note")]
    public string? SpecialDietNote { get; set; }

    [Column("profile_updated_at", TypeName = "timestamp with time zone")]
    public DateTime ProfileUpdatedAt { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
