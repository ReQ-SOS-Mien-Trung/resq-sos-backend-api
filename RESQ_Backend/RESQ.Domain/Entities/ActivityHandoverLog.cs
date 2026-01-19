using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Domain.Entities;

[Table("activity_handover_logs")]
public partial class ActivityHandoverLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("activity_id")]
    public int? ActivityId { get; set; }

    [Column("from_unit_id")]
    public int? FromUnitId { get; set; }

    [Column("to_unit_id")]
    public int? ToUnitId { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("decided_by")]
    public Guid? DecidedBy { get; set; }

    [ForeignKey("ActivityId")]
    [InverseProperty("ActivityHandoverLogs")]
    public virtual MissionActivity? Activity { get; set; }

    [ForeignKey("DecidedBy")]
    [InverseProperty("ActivityHandoverLogs")]
    public virtual User? DecidedByNavigation { get; set; }

    [ForeignKey("FromUnitId")]
    [InverseProperty("ActivityHandoverLogFromUnits")]
    public virtual RescueUnit? FromUnit { get; set; }

    [ForeignKey("ToUnitId")]
    [InverseProperty("ActivityHandoverLogToUnits")]
    public virtual RescueUnit? ToUnit { get; set; }
}
