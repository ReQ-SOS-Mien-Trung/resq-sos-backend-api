using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Entities.Operations;

[Table("conversations")]
public partial class Conversation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Victim là chủ sở hữu phòng chat. Mỗi victim có đúng 1 conversation.</summary>
    [Column("victim_id")]
    public Guid? VictimId { get; set; }

    /// <summary>Mission liên quan (optional) – coordinator có thể gán sau khi thống nhất hỗ trợ.</summary>
    [Column("mission_id")]
    public int? MissionId { get; set; }

    /// <summary>Trạng thái phòng chat: AiAssist | WaitingCoordinator | CoordinatorActive | Closed</summary>
    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    /// <summary>Topic mà victim đã chọn (ví dụ: "SosRequestSupport", "GeneralHelp").</summary>
    [Column("selected_topic")]
    [StringLength(100)]
    public string? SelectedTopic { get; set; }

    /// <summary>SOS Request mà victim chọn để hỗ trợ (nullable – chỉ set khi topic là SosRequestSupport).</summary>
    [Column("linked_sos_request_id")]
    public int? LinkedSosRequestId { get; set; }

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("VictimId")]
    [InverseProperty("OwnedConversations")]
    public virtual User? Victim { get; set; }

    [ForeignKey("MissionId")]
    [InverseProperty("Conversations")]
    public virtual Mission? Mission { get; set; }

    [InverseProperty("Conversation")]
    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    [InverseProperty("Conversation")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
