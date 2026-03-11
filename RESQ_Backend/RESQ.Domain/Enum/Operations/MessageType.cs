namespace RESQ.Domain.Enum.Operations;

/// <summary>
/// Loại tin nhắn trong conversation.
/// </summary>
public enum MessageType
{
    /// <summary>Tin nhắn do người dùng (Victim hoặc Coordinator) gửi.</summary>
    UserMessage,

    /// <summary>Tin nhắn do AI tạo ra (gợi ý chủ đề, danh sách SOS, v.v.).</summary>
    AiMessage,

    /// <summary>Tin nhắn hệ thống (Coordinator đã tham gia, phiên kết thúc, v.v.).</summary>
    SystemMessage
}
