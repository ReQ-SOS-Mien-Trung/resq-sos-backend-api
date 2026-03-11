using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SendMessage;

public class SendMessageCommandHandler(
    IConversationRepository conversationRepository,
    ILogger<SendMessageCommandHandler> logger
) : IRequestHandler<SendMessageCommand, SendMessageResponse>
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly ILogger<SendMessageCommandHandler> _logger = logger;

    public async Task<SendMessageResponse> Handle(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        // Verify sender is a participant
        var isParticipant = await _conversationRepository.IsParticipantAsync(
            request.ConversationId, request.SenderId, cancellationToken);

        if (!isParticipant)
            throw new ForbiddenException("Bạn không phải là thành viên của conversation này.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new BadRequestException("Nội dung tin nhắn không được để trống.");

        var message = await _conversationRepository.SendMessageAsync(
            request.ConversationId, request.SenderId, request.Content, request.MessageType, cancellationToken);

        _logger.LogInformation(
            "Message saved: ConversationId={conversationId}, SenderId={senderId}",
            request.ConversationId, request.SenderId);

        return new SendMessageResponse
        {
            Id = message.Id,
            ConversationId = request.ConversationId,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            Content = message.Content,
            MessageType = message.MessageType,
            CreatedAt = message.CreatedAt
        };
    }
}
