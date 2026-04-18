using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("check_in_radius_configs")]
public class CheckInRadiusConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("max_radius_meters")]
    public double MaxRadiusMeters { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}
