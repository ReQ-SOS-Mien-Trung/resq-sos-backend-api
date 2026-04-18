using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Logistics;

[Table("vat_invoice_items")]
public partial class VatInvoiceItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("vat_invoice_id")]
    public int? VatInvoiceId { get; set; }

    [Column("item_model_id")]
    public int? ItemModelId { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("unit_price", TypeName = "numeric(18,2)")]
    public decimal? UnitPrice { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("VatInvoiceId")]
    [InverseProperty("VatInvoiceItems")]
    public virtual VatInvoice? VatInvoice { get; set; }

    [ForeignKey("ItemModelId")]
    [InverseProperty("VatInvoiceItems")]
    public virtual ItemModel? ItemModel { get; set; }
}
