using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IConversationRepository
{
    // ─── Victim conversation ───────────────────────────────────────────────────

    /// <summary>
    /// Lấy conversation đang ở trạng thái AiAssist của victim, hoặc tạo mới.
    /// Mỗi lần victim chọn chủ đề, conversation chuyển trạng thái và lần gọi tiếp theo
    /// sẽ tạo ra một conversation mới — đảm bảo mỗi chủ đề là một đoạn chat riêng biệt.
    /// </summary>
    Task<ConversationModel> GetOrCreateForVictimAsync(Guid victimId, CancellationToken cancellationToken = default);

    /// <summary>Lấy tất cả conversations của một victim, sắp xếp mới nhất trước.</summary>
    Task<IEnumerable<ConversationModel>> GetVictimConversationsAsync(Guid victimId, CancellationToken cancellationToken = default);

    /// <summary>Lấy conversation của victim theo victimId (conversation mới nhất).</summary>
    Task<ConversationModel?> GetByVictimIdAsync(Guid victimId, CancellationToken cancellationToken = default);

    /// <summary>Lấy conversation theo Id (kèm participants).</summary>
    Task<ConversationModel?> GetByIdAsync(int conversationId, CancellationToken cancellationToken = default);

    // ─── Legacy: mission-based (coordinator xem danh sách) ───────────────────

    /// <summary>
    /// Lấy tất cả conversations của một mission mà user này tham gia.
    /// Coordinator sẽ thấy N conversations (1 per victim);
    /// Victim chỉ thấy 1 conversation của mình.
    /// </summary>
    Task<IEnumerable<ConversationModel>> GetAllByMissionIdForUserAsync(
        int missionId, Guid userId, CancellationToken cancellationToken = default);

    // ─── Status transitions ────────────────────────────────────────────────────

    /// <summary>Cập nhật trạng thái, topic và linked SOS request của conversation.</summary>
    Task UpdateStatusAsync(
        int conversationId,
        ConversationStatus status,
        string? selectedTopic = null,
        int? linkedSosRequestId = null,
        CancellationToken cancellationToken = default);

    // ─── Participants ──────────────────────────────────────────────────────────

    /// <summary>Kiểm tra người dùng có phải là participant của conversation không.</summary>
    Task<bool> IsParticipantAsync(int conversationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Thêm coordinator vào conversation (nếu chưa có). Role = "Coordinator".</summary>
    Task AddCoordinatorAsync(int conversationId, Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách conversation đang ở trạng thái WaitingCoordinator.</summary>
    Task<IEnumerable<ConversationModel>> GetConversationsWaitingForCoordinatorAsync(CancellationToken cancellationToken = default);

    // ─── Messages ─────────────────────────────────────────────────────────────

    /// <summary>Lấy danh sách tin nhắn của một conversation, sắp xếp cũ nhất trước.</summary>
    Task<IEnumerable<MessageModel>> GetMessagesAsync(int conversationId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Lưu tin nhắn mới và trả về model đầy đủ (kèm SenderName).</summary>
    Task<MessageModel> SendMessageAsync(
        int conversationId,
        Guid? senderId,
        string content,
        MessageType messageType = MessageType.UserMessage,
        CancellationToken cancellationToken = default);
}

