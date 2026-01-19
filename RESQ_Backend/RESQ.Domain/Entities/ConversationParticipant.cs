using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Domain.Entities;

[PrimaryKey("ConversationId", "UserId")]
[Table("conversation_participants")]
public partial class ConversationParticipant
{
    [Key]
    [Column("conversation_id")]
    public int ConversationId { get; set; }

    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role_in_conversation")]
    [StringLength(50)]
    public string? RoleInConversation { get; set; }

    [Column("joined_at", TypeName = "timestamp without time zone")]
    public DateTime? JoinedAt { get; set; }

    [Column("left_at", TypeName = "timestamp without time zone")]
    public DateTime? LeftAt { get; set; }

    [ForeignKey("ConversationId")]
    [InverseProperty("ConversationParticipants")]
    public virtual Conversation Conversation { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ConversationParticipants")]
    public virtual User User { get; set; } = null!;
}
