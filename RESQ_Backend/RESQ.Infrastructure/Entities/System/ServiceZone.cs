using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.System;

[Table("service_zones")]
public class ServiceZone
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Mảng tọa độ JSON: [{"lat": 10.3, "lon": 103.0}, ...]
    /// </summary>
    [Column("coordinates_json", TypeName = "jsonb")]
    public string CoordinatesJson { get; set; } = "[]";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
