using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace RESQ.Infrastructure.Entities;

[Table("sos_clusters")]
public partial class SosCluster
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("center_location", TypeName = "geography(Point,4326)")]
    public Point? CenterLocation { get; set; }

    [Column("radius_km")]
    public double? RadiusKm { get; set; }

    [Column("severity_level")]
    [StringLength(20)]
    public string? SeverityLevel { get; set; }

    [Column("water_level")]
    [StringLength(50)]
    public string? WaterLevel { get; set; }

    [Column("victim_estimated")]
    public int? VictimEstimated { get; set; }

    [Column("children_count")]
    public int? ChildrenCount { get; set; }

    [Column("elderly_count")]
    public int? ElderlyCount { get; set; }

    [Column("medical_urgency_score")]
    public double? MedicalUrgencyScore { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [InverseProperty("Cluster")]
    public virtual ICollection<ActivityAiSuggestion> ActivityAiSuggestions { get; set; } = new List<ActivityAiSuggestion>();

    [InverseProperty("Cluster")]
    public virtual ICollection<ClusterAiAnalysis> ClusterAiAnalyses { get; set; } = new List<ClusterAiAnalysis>();

    [InverseProperty("Cluster")]
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    [InverseProperty("Cluster")]
    public virtual ICollection<RescueUnitAiSuggestion> RescueUnitAiSuggestions { get; set; } = new List<RescueUnitAiSuggestion>();
}
