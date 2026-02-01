using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Identity;

[Table("rescuer_applications")]
public partial class RescuerApplication
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("submitted_at", TypeName = "timestamp with time zone")]
    public DateTime? SubmittedAt { get; set; }

    [Column("reviewed_at", TypeName = "timestamp with time zone")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [Column("admin_note")]
    public string? AdminNote { get; set; }

    [ForeignKey("ReviewedBy")]
    [InverseProperty("ReviewedApplications")]
    public virtual User? ReviewedByUser { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RescuerApplications")]
    public virtual User? User { get; set; }

    [InverseProperty("Application")]
    public virtual ICollection<RescuerApplicationDocument> RescuerApplicationDocuments { get; set; } = new List<RescuerApplicationDocument>();
}