using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("document_file_types")]
[Index("Code", Name = "document_file_types_code_key", IsUnique = true)]
public partial class DocumentFileType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(100)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(200)]
    public string? Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("document_file_type_category_id")]
    public int? DocumentFileTypeCategoryId { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("DocumentFileTypeCategoryId")]
    [InverseProperty("DocumentFileTypes")]
    public virtual DocumentFileTypeCategory? DocumentFileTypeCategory { get; set; }

    [InverseProperty("FileType")]
    public virtual ICollection<RescuerApplicationDocument> RescuerApplicationDocuments { get; set; } = new List<RescuerApplicationDocument>();
}
