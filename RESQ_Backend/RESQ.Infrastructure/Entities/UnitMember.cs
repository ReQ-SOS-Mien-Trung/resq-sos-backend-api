using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("unit_members")]
public partial class UnitMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("rescue_unit_id")]
    public int? RescueUnitId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [ForeignKey("RescueUnitId")]
    [InverseProperty("UnitMembers")]
    public virtual RescueUnit? RescueUnit { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UnitMembers")]
    public virtual User? User { get; set; }
}
