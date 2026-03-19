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
using RESQ.Infrastructure.Entities.Personnel;
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

    [Column("first_name")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [StringLength(100)]
    public string? LastName { get; set; }

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

    [Column("is_onboarded")]
    public bool IsOnboarded { get; set; } = false;

    [Column("is_eligible_rescuer")]
    public bool IsEligibleRescuer { get; set; } = false;

    [Column("avatar_url")]
    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    [Column("email_verification_token")]
    [StringLength(255)]
    public string? EmailVerificationToken { get; set; }

    [Column("email_verification_token_expiry", TypeName = "timestamp with time zone")]
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    [Column("password_reset_token")]
    [StringLength(255)]
    public string? PasswordResetToken { get; set; }

    [Column("password_reset_token_expiry", TypeName = "timestamp with time zone")]
    public DateTime? PasswordResetTokenExpiry { get; set; }

    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    [Column("refresh_token_expiry", TypeName = "timestamp with time zone")]
    public DateTime? RefreshTokenExpiry { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    [Column("ward")]
    [StringLength(100)]
    public string? Ward { get; set; }

    [Column("province")]
    [StringLength(100)]
    public string? Province { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("approved_at", TypeName = "timestamp with time zone")]
    public DateTime? ApprovedAt { get; set; }

    [Column("is_banned")]
    public bool IsBanned { get; set; } = false;

    [Column("banned_by")]
    public Guid? BannedBy { get; set; }

    [Column("banned_at", TypeName = "timestamp with time zone")]
    public DateTime? BannedAt { get; set; }

    [Column("ban_reason")]
    [StringLength(500)]
    public string? BanReason { get; set; }

    [Column("assembly_point_id")]
    public int? AssemblyPointId { get; set; }

    [ForeignKey("AssemblyPointId")]
    public virtual AssemblyPoint? AssignedAssemblyPoint { get; set; }

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

    /// <summary>Phòng chat của Victim (1-1: mỗi victim có đúng 1 conversation).</summary>
    [InverseProperty("Victim")]
    public virtual ICollection<Conversation> OwnedConversations { get; set; } = new List<Conversation>();

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

    [InverseProperty("RequestedByUser")]
    public virtual ICollection<DepotSupplyRequest> DepotSupplyRequests { get; set; } = new List<DepotSupplyRequest>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<CampaignDisbursement> CampaignDisbursements { get; set; } = new List<CampaignDisbursement>();

    [InverseProperty("RequestedByUser")]
    public virtual ICollection<FundingRequest> FundingRequestsCreated { get; set; } = new List<FundingRequest>();

    [InverseProperty("ReviewedByUser")]
    public virtual ICollection<FundingRequest> FundingRequestsReviewed { get; set; } = new List<FundingRequest>();
}
