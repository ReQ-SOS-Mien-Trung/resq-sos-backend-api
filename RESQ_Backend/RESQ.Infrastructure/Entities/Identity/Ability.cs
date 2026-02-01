using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("abilities")]
[Index("Code", Name = "abilities_code_key", IsUnique = true)]
public partial class Ability
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [InverseProperty("Ability")]
    public virtual ICollection<UserAbility> UserAbilities { get; set; } = new List<UserAbility>();
}
