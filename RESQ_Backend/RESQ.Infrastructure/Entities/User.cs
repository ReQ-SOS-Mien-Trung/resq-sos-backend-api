using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Entities;

[Table("users")]
[Index("Username", Name = "users_username_key", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("full_name")]
    [StringLength(255)]
    public string? FullName { get; set; }

    [Column("username")]
    [StringLength(100)]
    public string? Username { get; set; }

    [Column("email")]
    [StringLength(255)]
    public string? Email { get; set; }

    [Column("is_email_verified")]
    public bool IsEmailVerified { get; set; } = false;

    [Column("email_verification_token")]
    [StringLength(255)]
    public string? EmailVerificationToken { get; set; }

    [Column("email_verification_token_expiry", TypeName = "timestamp with time zone")]
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    [Column("phone")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column("password")]
    public string Password { get; set; } = null!;

    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    [Column("refresh_token_expiry", TypeName = "timestamp with time zone")]
    public DateTime? RefreshTokenExpiry { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("DecidedByNavigation")]
    public virtual ICollection<ActivityHandoverLog> ActivityHandoverLogs { get; set; } = new List<ActivityHandoverLog>();

    [InverseProperty("User")]
    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    [InverseProperty("DepotManager")]
    public virtual ICollection<Depot> Depots { get; set; } = new List<Depot>();

    [InverseProperty("PerformedByNavigation")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    [InverseProperty("Sender")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [InverseProperty("LastDecisionByNavigation")]
    public virtual ICollection<MissionActivity> MissionActivities { get; set; } = new List<MissionActivity>();

    [InverseProperty("Coordinator")]
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role? Role { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<SosRequest> SosRequests { get; set; } = new List<SosRequest>();

    [InverseProperty("User")]
    public virtual ICollection<UnitMember> UnitMembers { get; set; } = new List<UnitMember>();

    [InverseProperty("User")]
    public virtual ICollection<UserAbility> UserAbilities { get; set; } = new List<UserAbility>();
}
