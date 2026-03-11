using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;

public class CoordinatorJoinConversationCommandHandler(
    IConversationRepository conversationRepository,
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

        // Add coordinator as participant
        await conversationRepository.AddCoordinatorAsync(
            request.ConversationId, request.CoordinatorId, cancellationToken);

        // Transition to CoordinatorActive
        await conversationRepository.UpdateStatusAsync(
            request.ConversationId,
            ConversationStatus.CoordinatorActive,
            cancellationToken: cancellationToken);

        var systemMsg = "👤 Một Coordinator đã tham gia hỗ trợ bạn. " +
                        "Bạn có thể mô tả thêm nhu cầu và trao đổi trực tiếp.";

        await conversationRepository.SendMessageAsync(
            request.ConversationId,
            senderId: null,
            content: systemMsg,
            messageType: MessageType.SystemMessage,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Coordinator {CoordinatorId} joined Conversation {ConvId}",
            request.CoordinatorId, request.ConversationId);

        return new CoordinatorJoinConversationResponse
        {
            ConversationId = request.ConversationId,
            CoordinatorId = request.CoordinatorId,
            Status = ConversationStatus.CoordinatorActive,
            SystemMessage = systemMsg
        };
    }
}
