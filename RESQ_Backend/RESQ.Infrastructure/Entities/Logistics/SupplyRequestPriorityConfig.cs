using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("supply_request_priority_configs")]
public class SupplyRequestPriorityConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("urgent_minutes")]
    public int UrgentMinutes { get; set; }

    [Column("high_minutes")]
    public int HighMinutes { get; set; }

    [Column("medium_minutes")]
    public int MediumMinutes { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }
}
