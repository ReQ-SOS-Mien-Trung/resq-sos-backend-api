using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.LinkSosRequestToConversation;

public class LinkSosRequestToConversationCommandHandler(
    IConversationRepository conversationRepository,
    ISosRequestRepository sosRequestRepository,
    ILogger<LinkSosRequestToConversationCommandHandler> logger
) : IRequestHandler<LinkSosRequestToConversationCommand, LinkSosRequestToConversationResponse>
{
    public async Task<LinkSosRequestToConversationResponse> Handle(
        LinkSosRequestToConversationCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation {request.ConversationId} không tồn tại.");

        if (conversation.VictimId != request.VictimId)
            throw new ForbiddenException("Bạn không có quyền thao tác với conversation này.");

        // Verify SOS request belongs to this victim
        var sosRequest = await sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken)
            ?? throw new NotFoundException($"SOS Request {request.SosRequestId} không tồn tại.");

        if (sosRequest.UserId != request.VictimId)
            throw new ForbiddenException("Yêu cầu SOS không thuộc về bạn.");

        // Link SOS → conversation và chuyển sang WaitingCoordinator
        await conversationRepository.UpdateStatusAsync(
            request.ConversationId,
            ConversationStatus.WaitingCoordinator,
            selectedTopic: "SosRequestSupport",
            linkedSosRequestId: request.SosRequestId,
            cancellationToken: cancellationToken);

        var confirmMessage =
            $"✅ Bạn đã chọn yêu cầu SOS **#{sosRequest.Id}** ({sosRequest.SosType ?? "Chung"}) " +
            $"để được hỗ trợ.\n\n" +
            $"Nội dung: {sosRequest.RawMessage}\n\n" +
            $"Một Coordinator sẽ tham gia hỗ trợ bạn ngay. " +
            $"Trong lúc chờ, bạn có thể mô tả thêm nhu cầu của mình.";

        await conversationRepository.SendMessageAsync(
            request.ConversationId,
            senderId: null,
            content: confirmMessage,
            messageType: MessageType.AiMessage,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Victim {VictimId} linked SosRequest {SosId} to Conversation {ConvId}",
            request.VictimId, request.SosRequestId, request.ConversationId);

        return new LinkSosRequestToConversationResponse
        {
            ConversationId = request.ConversationId,
            LinkedSosRequestId = request.SosRequestId,
            Status = ConversationStatus.WaitingCoordinator,
            AiConfirmationMessage = confirmMessage
        };
    }
}
