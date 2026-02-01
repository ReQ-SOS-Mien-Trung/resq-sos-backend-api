using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Identity;

[PrimaryKey("RoleId", "ClaimId")]
[Table("role_permissions")]
public partial class RolePermission
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Key]
    [Column("claim_id")]
    public int ClaimId { get; set; }

    [Column("is_granted")]
    public bool? IsGranted { get; set; }

    [ForeignKey("ClaimId")]
    [InverseProperty("RolePermissions")]
    public virtual Permission Claim { get; set; } = null!;

    [ForeignKey("RoleId")]
    [InverseProperty("RolePermissions")]
    public virtual Role Role { get; set; } = null!;
}