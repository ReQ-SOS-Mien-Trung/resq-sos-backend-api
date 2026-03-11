using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.LinkSosRequestToConversation;

/// <summary>
/// Victim chọn một SOS Request cụ thể để hỗ trợ.
/// Sau bước này Coordinator mới được phép tham gia chat.
/// </summary>
public record LinkSosRequestToConversationCommand(
    int ConversationId,
    Guid VictimId,
    int SosRequestId
) : IRequest<LinkSosRequestToConversationResponse>;
