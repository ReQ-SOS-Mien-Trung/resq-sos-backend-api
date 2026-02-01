using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("roles")]
[Index("Name", Name = "roles_name_key", IsUnique = true)]
public partial class Role
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("Role")]
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    [InverseProperty("Role")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
