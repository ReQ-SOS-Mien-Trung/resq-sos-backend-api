using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("ability_categories")]
[Index("Code", Name = "ability_categories_code_key", IsUnique = true)]
public partial class AbilityCategory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [InverseProperty("AbilityCategory")]
    public virtual ICollection<AbilitySubgroup> Subgroups { get; set; } = new List<AbilitySubgroup>();
}
