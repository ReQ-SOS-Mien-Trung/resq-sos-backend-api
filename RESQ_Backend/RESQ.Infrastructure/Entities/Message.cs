using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("messages")]
public partial class Message
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("conversation_id")]
    public int? ConversationId { get; set; }

    [Column("sender_id")]
    public Guid? SenderId { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Column("sent_at", TypeName = "timestamp without time zone")]
    public DateTime? SentAt { get; set; }

    [ForeignKey("ConversationId")]
    [InverseProperty("Messages")]
    public virtual Conversation? Conversation { get; set; }

    [ForeignKey("SenderId")]
    [InverseProperty("Messages")]
    public virtual User? Sender { get; set; }
}
