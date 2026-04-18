using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;

public class CoordinatorJoinConversationCommandHandler(
    IConversationRepository conversationRepository,
    IUserRepository userRepository,
    IFirebaseService firebaseService,
    ILogger<CoordinatorJoinConversationCommandHandler> logger
) : IRequestHandler<CoordinatorJoinConversationCommand, CoordinatorJoinConversationResponse>
{
    public async Task<CoordinatorJoinConversationResponse> Handle(
        CoordinatorJoinConversationCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation {request.ConversationId} không tồn tại.");

        // Coordinator có thể join khi ở WaitingCoordinator hoặc CoordinatorActive
        if (conversation.Status == ConversationStatus.Closed)
            throw new BadRequestException("Phiên hỗ trợ này đã kết thúc.");

        if (conversation.Status == ConversationStatus.AiAssist)
            throw new BadRequestException(
                "Victim chưa chọn chủ đề hỗ trợ. Vui lòng chờ victim xác nhận yêu cầu.");

        var coordinator = await userRepository.GetByIdAsync(request.CoordinatorId, cancellationToken);
        var coordinatorName = coordinator != null
            ? $"{coordinator.LastName} {coordinator.FirstName}".Trim()
            : "Coordinator";

        var systemMsg = $"👤 {coordinatorName} đã tham gia hỗ trợ bạn. " +
                        "Bạn có thể mô tả thêm nhu cầu và trao đổi trực tiếp.";

        var isParticipant = await conversationRepository.IsParticipantAsync(
            request.ConversationId, request.CoordinatorId, cancellationToken);

        if (!isParticipant)
        {
            // Add coordinator as participant
            await conversationRepository.AddCoordinatorAsync(
                request.ConversationId, request.CoordinatorId, cancellationToken);

            // Transition to CoordinatorActive
            await conversationRepository.UpdateStatusAsync(
                request.ConversationId,
                ConversationStatus.CoordinatorActive,
                cancellationToken: cancellationToken);

            await conversationRepository.SendMessageAsync(
                request.ConversationId,
                senderId: null,
                content: systemMsg,
                messageType: MessageType.SystemMessage,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Coordinator {CoordinatorId} joined Conversation {ConvId}",
                request.CoordinatorId, request.ConversationId);

            // Push notification đến victim
            if (conversation.VictimId.HasValue)
            {
                await firebaseService.SendNotificationToUserAsync(
                    conversation.VictimId.Value,
                    "Coordinator đã tham gia",
                    $"{coordinatorName} đã tham gia hỗ trợ bạn. Bạn có thể mô tả thêm nhu cầu của mình.",
                    "coordinator_join",
                    cancellationToken);
            }
        }
        else
        {
            logger.LogInformation(
                "Coordinator {CoordinatorId} already joined Conversation {ConvId}. System message and notification skipped.",
                request.CoordinatorId, request.ConversationId);
        }

        return new CoordinatorJoinConversationResponse
        {
            ConversationId = request.ConversationId,
            CoordinatorId = request.CoordinatorId,
            Status = ConversationStatus.CoordinatorActive,
            SystemMessage = systemMsg
        };
    }
}
