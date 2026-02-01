using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depot_managers")]
public partial class DepotManager
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("assigned_at", TypeName = "timestamp with time zone")]
    public DateTime? AssignedAt { get; set; }

    [Column("unassigned_at", TypeName = "timestamp with time zone")]
    public DateTime? UnassignedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("DepotManagers")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("DepotManagers")]
    public virtual User? User { get; set; }
}