using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.SelectSupportTopic;

/// <summary>
/// Victim chọn một chủ đề hỗ trợ.
/// - Nếu TopicKey = "SosRequestSupport": AI truy vấn SOS requests của victim và trả về danh sách.
/// - Các topic khác: AI ghi nhận và chuyển trạng thái WaitingCoordinator ngay.
/// </summary>
public record SelectSupportTopicCommand(
    int ConversationId,
    Guid VictimId,
    string TopicKey
) : IRequest<SelectSupportTopicResponse>;
