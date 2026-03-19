using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Personnel;

[Table("assembly_participants")]
public class AssemblyParticipant
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("assembly_event_id")]
    public int AssemblyEventId { get; set; }

    [Column("rescuer_id")]
    public Guid RescuerId { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "Assigned";

    [Column("is_checked_in")]
    public bool IsCheckedIn { get; set; }

    [Column("check_in_time", TypeName = "timestamp with time zone")]
    public DateTime? CheckInTime { get; set; }

    [ForeignKey("AssemblyEventId")]
    [InverseProperty("Participants")]
    public virtual AssemblyEvent? AssemblyEvent { get; set; }

    [ForeignKey("RescuerId")]
    public virtual User? Rescuer { get; set; }
}
