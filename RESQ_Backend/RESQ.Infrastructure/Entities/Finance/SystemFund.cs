using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

/// <summary>
/// Quỹ hệ thống (singleton) - chứa tiền thanh lý tài sản khi đóng kho.
/// Admin có thể cấp tiền từ quỹ này cho kho.
/// </summary>
[Table("system_funds")]
public class SystemFund
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = "Quỹ hệ thống";

    [Column("balance")]
    public decimal Balance { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime LastUpdatedAt { get; set; }
}
