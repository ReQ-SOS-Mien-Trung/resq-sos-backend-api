using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class MessageModel
{
    public int Id { get; set; }
    public int? ConversationId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }

    /// <summary>Phân loại tin nhắn: UserMessage | AiMessage | SystemMessage.</summary>
    public MessageType MessageType { get; set; } = MessageType.UserMessage;

    public DateTime? CreatedAt { get; set; }
}
