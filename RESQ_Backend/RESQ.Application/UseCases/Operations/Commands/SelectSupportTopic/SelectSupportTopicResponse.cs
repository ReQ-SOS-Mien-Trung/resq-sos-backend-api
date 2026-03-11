using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SelectSupportTopic;

public class SelectSupportTopicResponse
{
    public int ConversationId { get; set; }
    public ConversationStatus Status { get; set; }
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>Tin nhắn AI phản hồi (ví dụ: danh sách SOS hoặc xác nhận chủ đề).</summary>
    public string AiResponseMessage { get; set; } = string.Empty;

    /// <summary>Danh sách SOS requests (chỉ có giá trị khi TopicKey = "SosRequestSupport").</summary>
    public List<SosRequestDto>? SosRequests { get; set; }
}
