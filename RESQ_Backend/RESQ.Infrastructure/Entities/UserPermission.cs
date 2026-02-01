using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[PrimaryKey("UserId", "ClaimId")]
[Table("user_permissions")]
public partial class UserPermission
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Key]
    [Column("claim_id")]
    public int ClaimId { get; set; }

    [Column("is_granted")]
    public bool? IsGranted { get; set; }

    [Column("granted_by")]
    public Guid? GrantedBy { get; set; }

    [Column("granted_at", TypeName = "timestamp with time zone")]
    public DateTime? GrantedAt { get; set; }

    [ForeignKey("ClaimId")]
    [InverseProperty("UserPermissions")]
    public virtual Permission Claim { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserPermissions")]
    public virtual User User { get; set; } = null!;
}