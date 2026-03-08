namespace RESQ.Domain.Enum.Operations;

/// <summary>
/// Trạng thái phòng chat của Victim.
/// </summary>
public enum ConversationStatus
{
    /// <summary>AI đang hỗ trợ, gợi ý chủ đề — chưa có Coordinator tham gia.</summary>
    AiAssist,

    /// <summary>Victim đã chọn SOS Request, đang chờ Coordinator tham gia.</summary>
    WaitingCoordinator,

    /// <summary>Coordinator đã tham gia, đang hỗ trợ trực tiếp.</summary>
    CoordinatorActive,

    /// <summary>Phiên hỗ trợ đã kết thúc.</summary>
    Closed
}
