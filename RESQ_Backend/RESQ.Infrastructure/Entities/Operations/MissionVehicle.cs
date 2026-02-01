using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("mission_vehicles")]
public partial class MissionVehicle
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("vehicle_id")]
    public int? VehicleId { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("reserved_at", TypeName = "timestamp with time zone")]
    public DateTime? ReservedAt { get; set; }

    [Column("released_at", TypeName = "timestamp with time zone")]
    public DateTime? ReleasedAt { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("MissionVehicles")]
    public virtual Mission? Mission { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("MissionVehicles")]
    public virtual Vehicle? Vehicle { get; set; }
}