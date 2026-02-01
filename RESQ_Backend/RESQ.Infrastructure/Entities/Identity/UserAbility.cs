using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Identity;

[PrimaryKey("UserId", "AbilityId")]
[Table("user_abilities")]
public partial class UserAbility
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Key]
    [Column("ability_id")]
    public int AbilityId { get; set; }

    [Column("level")]
    public int? Level { get; set; }

    [ForeignKey("AbilityId")]
    [InverseProperty("UserAbilities")]
    public virtual Ability Ability { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserAbilities")]
    public virtual User User { get; set; } = null!;
}
