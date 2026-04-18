using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

/// <summary>
/// AI service hỗ trợ luồng chat của Victim:
/// 1. Gợi ý chủ đề nhanh (topic suggestions)
/// 2. Khi victim chọn "SOS Request Support" → AI truy vấn và format danh sách SOS
/// </summary>
public interface IChatSupportAiService
{
    /// <summary>
    /// Sinh danh sách chủ đề gợi ý nhanh khi victim mở chat lần đầu.
    /// Ví dụ: "Hỗ trợ theo yêu cầu SOS", "Câu hỏi về nhu yếu phẩm", "Tình trạng khẩn cấp"...
    /// </summary>
    Task<ChatTopicSuggestionsResult> GetTopicSuggestionsAsync(
        Guid victimId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Format danh sách SOS request của victim thành tin nhắn AI để hiển thị trong chat.
    /// Victim sẽ chọn SOS request muốn được hỗ trợ.
    /// </summary>
    Task<string> FormatSosRequestListMessageAsync(
        IEnumerable<SosRequestModel> sosRequests,
        CancellationToken cancellationToken = default);
}

public class ChatTopicSuggestionsResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ChatTopicSuggestion> Topics { get; set; } = [];
    public string? AiGreetingMessage { get; set; }
}

public class ChatTopicSuggestion
{
    /// <summary>Mã topic nội bộ, ví dụ: "SosRequestSupport", "GeneralHelp", "SupplyRequest".</summary>
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>Nhãn hiển thị cho người dùng.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn về chủ đề.</summary>
    public string? Description { get; set; }

    /// <summary>Icon hoặc emoji gợi ý.</summary>
    public string? Icon { get; set; }
}
