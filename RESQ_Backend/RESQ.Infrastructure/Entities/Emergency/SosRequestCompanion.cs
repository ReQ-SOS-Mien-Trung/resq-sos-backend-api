using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Emergency;

[Table("sos_request_companions")]
[Index(nameof(SosRequestId), nameof(UserId), IsUnique = true, Name = "ix_sos_request_companions_request_user")]
public class SosRequestCompanion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("sos_request_id")]
    public int SosRequestId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("phone_number")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("added_at", TypeName = "timestamp with time zone")]
    public DateTime AddedAt { get; set; }

    [ForeignKey("SosRequestId")]
    [InverseProperty("Companions")]
    public virtual SosRequest SosRequest { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("CompanionSosRequests")]
    public virtual User User { get; set; } = null!;
}
