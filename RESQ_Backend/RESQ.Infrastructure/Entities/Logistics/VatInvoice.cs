using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("vat_invoices")]
public partial class VatInvoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("invoice_number")]
    [StringLength(50)]
    public string? InvoiceNumber { get; set; }

    [Column("supplier_name")]
    [StringLength(255)]
    public string? SupplierName { get; set; }

    [Column("supplier_tax_code")]
    [StringLength(50)]
    public string? SupplierTaxCode { get; set; }

    [Column("invoice_date")]
    public DateOnly? InvoiceDate { get; set; }

    [Column("total_amount")]
    public decimal? TotalAmount { get; set; }

    [Column("file_url")]
    public string? FileUrl { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("VatInvoice")]
    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    [InverseProperty("VatInvoice")]
    public virtual ICollection<VehicleActivityLog> VehicleActivityLogs { get; set; } = new List<VehicleActivityLog>();
}