using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Enum.Emergency;
using System.Text;

namespace RESQ.Infrastructure.Services;

/// <summary>
/// Triển khai IChatSupportAiService.
/// Phiên bản hiện tại sử dụng logic tĩnh để sinh gợi ý,
/// có thể mở rộng để gọi Gemini API khi cần.
/// </summary>
public class ChatSupportAiService(ILogger<ChatSupportAiService> logger) : IChatSupportAiService
{
    private readonly ILogger<ChatSupportAiService> _logger = logger;

    private static readonly List<ChatTopicSuggestion> DefaultTopics =
    [
        new()
        {
            TopicKey = "SosRequestSupport",
            Label = "Hỗ trợ theo yêu cầu SOS",
            Description = "Xem và bổ sung yêu cầu đang được xử lý",
            Icon = "🆘"
        },
        new()
        {
            TopicKey = "SupplyRequest",
            Label = "Yêu cầu nhu yếu phẩm",
            Description = "Thực phẩm, nước uống, thuốc men...",
            Icon = "🧃"
        },
        new()
        {
            TopicKey = "MedicalHelp",
            Label = "Hỗ trợ y tế",
            Description = "Sơ cứu, thuốc men, tình trạng sức khỏe",
            Icon = "🏥"
        },
        new()
        {
            TopicKey = "LocationUpdate",
            Label = "Cập nhật vị trí",
            Description = "Vị trí hiện tại của tôi đã thay đổi",
            Icon = "📍"
        },
        new()
        {
            TopicKey = "GeneralHelp",
            Label = "Câu hỏi khác",
            Description = "Tôi cần hỗ trợ về vấn đề khác",
            Icon = "💬"
        }
    ];

    public Task<ChatTopicSuggestionsResult> GetTopicSuggestionsAsync(
        Guid victimId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating chat topic suggestions for victim {VictimId}", victimId);

        var result = new ChatTopicSuggestionsResult
        {
            IsSuccess = true,
            AiGreetingMessage =
                "Xin chào! Tôi là trợ lý hỗ trợ của RESQ. " +
                "Tôi có thể giúp bạn với các vấn đề sau. " +
                "Vui lòng chọn chủ đề phù hợp:",
            Topics = DefaultTopics
        };

        return Task.FromResult(result);
    }

    public Task<string> FormatSosRequestListMessageAsync(
        IEnumerable<SosRequestModel> sosRequests,
        CancellationToken cancellationToken = default)
    {
        var requests = sosRequests.ToList();

        if (requests.Count == 0)
        {
            return Task.FromResult(
                "Hiện tại bạn không có yêu cầu SOS nào đang được xử lý. " +
                "Nếu bạn cần hỗ trợ khẩn cấp, vui lòng gửi yêu cầu SOS mới.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("📋 **Danh sách yêu cầu SOS của bạn:**");
        sb.AppendLine();

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            var statusLabel = req.Status switch
            {
                SosRequestStatus.Pending    => "⏳ Đang chờ",
                SosRequestStatus.Assigned   => "✅ Đã phân công",
                SosRequestStatus.InProgress => "🔄 Đang xử lý",
                SosRequestStatus.Resolved   => "✔️ Đã giải quyết",
                SosRequestStatus.Cancelled  => "❌ Đã hủy",
                _                           => req.Status.ToString()
            };

            var priority = req.PriorityLevel.HasValue
                ? $" | Ưu tiên: {req.PriorityLevel}"
                : string.Empty;

            sb.AppendLine($"**{i + 1}. [ID: {req.Id}]** {req.SosType ?? "Chung"}");
            sb.AppendLine($"   Trạng thái: {statusLabel}{priority}");
            sb.AppendLine($"   Nội dung: {TruncateMessage(req.RawMessage, 80)}");

            if (req.CreatedAt.HasValue)
                sb.AppendLine($"   Gửi lúc: {req.CreatedAt.Value:dd/MM/yyyy HH:mm}");

            sb.AppendLine();
        }

        sb.AppendLine("Vui lòng cho tôi biết bạn muốn hỗ trợ yêu cầu nào " +
                      "(nhập số thứ tự hoặc ID của yêu cầu).");

        return Task.FromResult(sb.ToString());
    }

    private static string TruncateMessage(string message, int maxLength) =>
        message.Length <= maxLength ? message : message[..maxLength] + "...";
}
