using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("vehicles")]
public partial class Vehicle
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("depot_id")]
    public int? DepotId { get; set; }

    [Column("vehicle_categories_id")]
    public int? VehicleCategoryId { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("condition")]
    public string? Condition { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("DepotId")]
    [InverseProperty("Vehicles")]
    public virtual Depot? Depot { get; set; }

    [ForeignKey("VehicleCategoryId")]
    [InverseProperty("Vehicles")]
    public virtual VehicleCategory? VehicleCategory { get; set; }

    [InverseProperty("Vehicle")]
    public virtual ICollection<MissionVehicle> MissionVehicles { get; set; } = new List<MissionVehicle>();

    [InverseProperty("Vehicle")]
    public virtual ICollection<VehicleActivityLog> VehicleActivityLogs { get; set; } = new List<VehicleActivityLog>();
}