using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("ability_subgroups")]
[Index("Code", Name = "ability_subgroups_code_key", IsUnique = true)]
public partial class AbilitySubgroup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("ability_category_id")]
    public int? AbilityCategoryId { get; set; }

    [ForeignKey("AbilityCategoryId")]
    [InverseProperty("Subgroups")]
    public virtual AbilityCategory? AbilityCategory { get; set; }

    [InverseProperty("AbilitySubgroup")]
    public virtual ICollection<Ability> Abilities { get; set; } = new List<Ability>();
}
