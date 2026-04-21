using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Personnel;

/// <summary>
/// Cấu hình bán kính check-in riêng cho từng điểm tập kết.
/// Nếu tồn tại, sẽ được ưu tiên so với cấu hình toàn cục (check_in_radius_configs).
/// </summary>
[Table("assembly_point_check_in_radius_configs")]
public class AssemblyPointCheckInRadiusConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>FK tới assembly_points. Unique — mỗi điểm tập kết chỉ có 1 cấu hình riêng.</summary>
    [Column("assembly_point_id")]
    public int AssemblyPointId { get; set; }

    [Column("max_radius_meters")]
    public double MaxRadiusMeters { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(AssemblyPointId))]
    [InverseProperty("CheckInRadiusConfig")]
    public virtual AssemblyPoint AssemblyPoint { get; set; } = null!;
}
