using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("sos_ai_analysis")]
public partial class SosAiAnalysis
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sos_request_id")]
    public int? SosRequestId { get; set; }

    [Column("extracted_data", TypeName = "jsonb")]
    public string? ExtractedData { get; set; }

    [Column("model_version")]
    [StringLength(50)]
    public string? ModelVersion { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("SosRequestId")]
    [InverseProperty("SosAiAnalyses")]
    public virtual SosRequest? SosRequest { get; set; }
}
