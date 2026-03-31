using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

/// <summary>
/// Cấu hình warning bands cho hệ thống cảnh báo tồn kho.
/// Chỉ có 1 row duy nhất (id = 1), overwrite khi cập nhật.
/// </summary>
[Table("stock_warning_band_config")]
public class StockWarningBandConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// JSON array của warning bands. Format:
    /// [{ "name": "CRITICAL", "from": 0.0, "to": 0.4 }, ...]
    /// </summary>
    [Column("bands_json", TypeName = "jsonb")]
    public string BandsJson { get; set; } = "[]";

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}
