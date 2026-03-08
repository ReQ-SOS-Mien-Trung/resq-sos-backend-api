using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;

/// <summary>
/// Coordinator chủ động tham gia phòng chat của Victim để hỗ trợ.
/// Conversation phải ở trạng thái WaitingCoordinator.
/// </summary>
public record CoordinatorJoinConversationCommand(
    int ConversationId,
    Guid CoordinatorId
) : IRequest<CoordinatorJoinConversationResponse>;
