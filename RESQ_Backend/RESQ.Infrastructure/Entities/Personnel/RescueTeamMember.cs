using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Personnel;

[Table("rescue_team_members")]
[PrimaryKey("TeamId", "UserId")]
public class RescueTeamMember
{
    [Key]
    [Column("team_id")]
    public int TeamId { get; set; }

    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    [Column("invited_at", TypeName = "timestamp with time zone")]
    public DateTime InvitedAt { get; set; }

    [Column("responded_at", TypeName = "timestamp with time zone")]
    public DateTime? RespondedAt { get; set; }

    [Column("is_leader")]
    public bool IsLeader { get; set; }

    [Column("role_in_team")]
    [StringLength(100)]
    public string? RoleInTeam { get; set; }

    [Column("checked_in")]
    public bool CheckedIn { get; set; }

    [ForeignKey("TeamId")]
    public virtual RescueTeam? Team { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
