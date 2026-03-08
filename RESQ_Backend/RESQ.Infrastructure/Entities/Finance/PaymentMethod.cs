using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.Finance;

[Table("payment_methods")]
public class PaymentMethod
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [InverseProperty("PaymentMethod")]
    public virtual ICollection<Donation> Donations { get; set; } = new List<Donation>();
}