using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Personnel;

[Table("assembly_events")]
public class AssemblyEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("assembly_point_id")]
    public int AssemblyPointId { get; set; }

    [Column("assembly_date", TypeName = "timestamp with time zone")]
    public DateTime AssemblyDate { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "Gathering";

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Thời hạn check-in. Sau thời điểm này rescuer không thể check-in (nhưng vẫn có thể check-out).</summary>
    [Column("check_in_deadline", TypeName = "timestamp with time zone")]
    public DateTime? CheckInDeadline { get; set; }

    [ForeignKey("AssemblyPointId")]
    public virtual AssemblyPoint? AssemblyPoint { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [InverseProperty("AssemblyEvent")]
    public virtual ICollection<AssemblyParticipant> Participants { get; set; } = new List<AssemblyParticipant>();
}
