using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorLeaveConversation;

/// <summary>
/// Coordinator rời khỏi phòng chat đang hỗ trợ.
/// Conversation sẽ chuyển về WaitingCoordinator để coordinator khác có thể tiếp nhận.
/// </summary>
public record CoordinatorLeaveConversationCommand(
    int ConversationId,
    Guid CoordinatorId
) : IRequest<CoordinatorLeaveConversationResponse>;
