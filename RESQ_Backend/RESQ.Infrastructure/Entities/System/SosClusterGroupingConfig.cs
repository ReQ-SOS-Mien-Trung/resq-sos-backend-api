using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("sos_cluster_grouping_configs")]
public class SosClusterGroupingConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("maximum_distance_km")]
    public double MaximumDistanceKm { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}