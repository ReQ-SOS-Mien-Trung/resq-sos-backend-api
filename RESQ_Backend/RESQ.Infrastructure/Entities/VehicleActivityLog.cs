using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("vehicle_activity_logs")]
public partial class VehicleActivityLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("vehicle_id")]
    public int? VehicleId { get; set; }

    [Column("vat_invoice_id")]
    public int? VatInvoiceId { get; set; }

    [Column("movement_type")]
    [StringLength(50)]
    public string? MovementType { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [Column("from_depot_id")]
    public int? FromDepotId { get; set; }

    [Column("to_depot_id")]
    public int? ToDepotId { get; set; }

    [Column("moved_at", TypeName = "timestamp with time zone")]
    public DateTime? MovedAt { get; set; }

    [Column("performed_by")]
    public Guid? PerformedBy { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("PerformedBy")]
    [InverseProperty("VehicleActivityLogs")]
    public virtual User? PerformedByUser { get; set; }

    [ForeignKey("VatInvoiceId")]
    [InverseProperty("VehicleActivityLogs")]
    public virtual VatInvoice? VatInvoice { get; set; }

    [ForeignKey("VehicleId")]
    [InverseProperty("VehicleActivityLogs")]
    public virtual Vehicle? Vehicle { get; set; }
}