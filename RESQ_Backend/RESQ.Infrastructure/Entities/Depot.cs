using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Domain.Entities;

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

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("current_utilization")]
    public int? CurrentUtilization { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [Column("depot_manager_id")]
    public Guid? DepotManagerId { get; set; }

    [InverseProperty("Depot")]
    public virtual ICollection<DepotInventory> DepotInventories { get; set; } = new List<DepotInventory>();

    [ForeignKey("DepotManagerId")]
    [InverseProperty("Depots")]
    public virtual User? DepotManager { get; set; }

    [InverseProperty("SourceDepot")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();
}
