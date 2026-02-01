using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities;

[Table("depots")]
public partial class Depot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("current_utilization")]
    public int? CurrentUtilization { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [InverseProperty("Depot")]
    public virtual ICollection<DepotFundAllocation> DepotFundAllocations { get; set; } = new List<DepotFundAllocation>();

    [InverseProperty("Depot")]
    public virtual ICollection<DepotManager> DepotManagers { get; set; } = new List<DepotManager>();

    [InverseProperty("Depot")]
    public virtual ICollection<DepotSupplyInventory> DepotSupplyInventories { get; set; } = new List<DepotSupplyInventory>();

    [InverseProperty("SourceDepot")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [InverseProperty("Depot")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
