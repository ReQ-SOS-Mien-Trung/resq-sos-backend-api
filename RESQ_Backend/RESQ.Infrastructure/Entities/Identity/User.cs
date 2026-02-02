using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.System; // Added to resolve Message

namespace RESQ.Infrastructure.Entities.Identity;

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

    [Column("phone")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column("password")]
    public string Password { get; set; } = null!;

    [Column("rescuer_type")]
    [StringLength(50)]
    public string? RescuerType { get; set; }

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

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("approved_at", TypeName = "timestamp with time zone")]
    public DateTime? ApprovedAt { get; set; }

    [ForeignKey("ApprovedBy")]
    [InverseProperty("ApprovedUsers")]
    public virtual User? ApprovedByUser { get; set; }

    [InverseProperty("ApprovedByUser")]
    public virtual ICollection<User> ApprovedUsers { get; set; } = new List<User>();

    [InverseProperty("User")]
    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    [InverseProperty("User")]
    public virtual ICollection<DepotManager> DepotManagers { get; set; } = new List<DepotManager>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<FundCampaign> FundCampaigns { get; set; } = new List<FundCampaign>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<FundTransaction> FundTransactions { get; set; } = new List<FundTransaction>();

    [InverseProperty("PerformedByUser")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    [InverseProperty("LastDecisionByUser")]
    public virtual ICollection<MissionActivity> MissionActivities { get; set; } = new List<MissionActivity>();

    [InverseProperty("CreatedBy")]
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    [InverseProperty("Rescuer")]
    public virtual ICollection<MissionTeamMember> MissionTeamMembers { get; set; } = new List<MissionTeamMember>();

    // FIXED: Added Messages collection to resolve InverseProperty("Messages") on Message.Sender
    [InverseProperty("Sender")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("AllocatedByUser")]
    public virtual ICollection<DepotFundAllocation> DepotFundAllocations { get; set; } = new List<DepotFundAllocation>();

    [InverseProperty("User")]
    public virtual ICollection<RescuerApplication> RescuerApplications { get; set; } = new List<RescuerApplication>();

    [InverseProperty("ReviewedByUser")]
    public virtual ICollection<RescuerApplication> ReviewedApplications { get; set; } = new List<RescuerApplication>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role? Role { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<SosRequest> SosRequests { get; set; } = new List<SosRequest>();

    [InverseProperty("ReviewedBy")]
    public virtual ICollection<SosRequest> ReviewedSosRequests { get; set; } = new List<SosRequest>();

    [InverseProperty("User")]
    public virtual ICollection<UserAbility> UserAbilities { get; set; } = new List<UserAbility>();

    [InverseProperty("User")]
    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();

    [InverseProperty("User")]
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();

    [InverseProperty("PerformedByUser")]
    public virtual ICollection<VehicleActivityLog> VehicleActivityLogs { get; set; } = new List<VehicleActivityLog>();
}
