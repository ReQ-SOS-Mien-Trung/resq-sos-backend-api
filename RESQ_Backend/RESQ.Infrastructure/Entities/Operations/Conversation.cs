using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("conversations")]
public partial class Conversation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mission_id")]
    public int? MissionId { get; set; }

    [InverseProperty("Conversation")]
    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    [InverseProperty("Conversation")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [ForeignKey("MissionId")]
    [InverseProperty("Conversations")]
    public virtual Mission? Mission { get; set; }
}
