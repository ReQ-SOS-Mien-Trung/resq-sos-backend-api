using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("rescuer_application_documents")]
public partial class RescuerApplicationDocument
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("application_id")]
    public int? ApplicationId { get; set; }

    [Column("file_url")]
    public string? FileUrl { get; set; }

    [Column("file_type")]
    [StringLength(50)]
    public string? FileType { get; set; }

    [Column("uploaded_at", TypeName = "timestamp with time zone")]
    public DateTime? UploadedAt { get; set; }

    [ForeignKey("ApplicationId")]
    [InverseProperty("RescuerApplicationDocuments")]
    public virtual RescuerApplication? Application { get; set; }
}