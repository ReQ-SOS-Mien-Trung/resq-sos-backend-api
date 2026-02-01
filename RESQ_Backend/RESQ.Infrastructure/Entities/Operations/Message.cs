using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Operations;

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

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ConversationId")]
    [InverseProperty("Messages")]
    public virtual Conversation? Conversation { get; set; }

    [ForeignKey("SenderId")]
    [InverseProperty("Messages")]
    public virtual User? Sender { get; set; }
}
