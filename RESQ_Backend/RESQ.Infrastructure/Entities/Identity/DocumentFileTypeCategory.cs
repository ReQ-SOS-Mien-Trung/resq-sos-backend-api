using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("document_file_type_categories")]
[Index("Code", Name = "document_file_type_categories_code_key", IsUnique = true)]
public partial class DocumentFileTypeCategory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [InverseProperty("DocumentFileTypeCategory")]
    public virtual ICollection<DocumentFileType> DocumentFileTypes { get; set; } = new List<DocumentFileType>();
}
