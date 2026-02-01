using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("sos_request_updates")]
public partial class SosRequestUpdate
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sos_request_id")]
    public int? SosRequestId { get; set; }

    [Column("type")]
    [StringLength(50)]
    public string? Type { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [ForeignKey("SosRequestId")]
    [InverseProperty("SosRequestUpdates")]
    public virtual SosRequest? SosRequest { get; set; }
}