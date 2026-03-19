using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorLeaveConversation;

public class CoordinatorLeaveConversationCommandHandler(
    IConversationRepository conversationRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    ILogger<CoordinatorLeaveConversationCommandHandler> logger
) : IRequestHandler<CoordinatorLeaveConversationCommand, CoordinatorLeaveConversationResponse>
{
    public async Task<CoordinatorLeaveConversationResponse> Handle(
        CoordinatorLeaveConversationCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation {request.ConversationId} không tồn tại.");

        if (conversation.Status == ConversationStatus.Closed)
            throw new BadRequestException("Phiên hỗ trợ này đã kết thúc.");

        var isParticipant = await conversationRepository.IsParticipantAsync(
            request.ConversationId, request.CoordinatorId, cancellationToken);

        if (!isParticipant)
            throw new BadRequestException("Bạn không phải là thành viên của conversation này.");

        // Xóa coordinator khỏi danh sách participant
        await conversationRepository.RemoveCoordinatorAsync(
            request.ConversationId, request.CoordinatorId, cancellationToken);

        // Chuyển trạng thái về WaitingCoordinator để coordinator khác có thể tiếp nhận
        var newStatus = ConversationStatus.WaitingCoordinator;
        await conversationRepository.UpdateStatusAsync(
            request.ConversationId, newStatus, cancellationToken: cancellationToken);

        var coordinator = await userRepository.GetByIdAsync(request.CoordinatorId, cancellationToken);
        var coordinatorName = coordinator != null
            ? $"{coordinator.FirstName} {coordinator.LastName}".Trim()
            : "Coordinator";

        var systemMsg = $"👤 {coordinatorName} đã rời phòng chat. Vui lòng chờ coordinator khác tham gia hỗ trợ bạn.";

        await conversationRepository.SendMessageAsync(
            request.ConversationId,
            senderId: null,
            content: systemMsg,
            messageType: MessageType.SystemMessage,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Coordinator {CoordinatorId} left Conversation {ConvId}",
            request.CoordinatorId, request.ConversationId);

        // Push notification đến victim
        if (conversation.VictimId.HasValue)
        {
            await firebaseService.SendNotificationToUserAsync(
                conversation.VictimId.Value,
                "Coordinator đã rời phòng",
                $"{coordinatorName} đã rời phòng chat. Vui lòng chờ coordinator khác tham gia hỗ trợ bạn.",
                "coordinator_leave",
                cancellationToken);
        }

        return new CoordinatorLeaveConversationResponse
        {
            ConversationId = request.ConversationId,
            CoordinatorId = request.CoordinatorId,
            Status = newStatus,
            SystemMessage = systemMsg
        };
    }
}
