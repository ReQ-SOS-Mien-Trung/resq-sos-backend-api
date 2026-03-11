using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationMessages;

public class GetConversationMessagesQueryHandler(
    IConversationRepository conversationRepository,
    ILogger<GetConversationMessagesQueryHandler> logger
) : IRequestHandler<GetConversationMessagesQuery, GetConversationMessagesResponse>
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly ILogger<GetConversationMessagesQueryHandler> _logger = logger;

    public async Task<GetConversationMessagesResponse> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting messages for ConversationId={conversationId}, Page={page}",
            request.ConversationId, request.Page);

        // Verify the requester is a participant
        var isParticipant = await _conversationRepository.IsParticipantAsync(
            request.ConversationId, request.RequesterId, cancellationToken);

        if (!isParticipant)
            throw new ForbiddenException("Bạn không phải là thành viên của conversation này.");

        var messages = await _conversationRepository.GetMessagesAsync(
            request.ConversationId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new GetConversationMessagesResponse
        {
            ConversationId = request.ConversationId,
            Page = request.Page,
            PageSize = request.PageSize,
            Messages = messages.Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                Content = m.Content,
                MessageType = m.MessageType,
                CreatedAt = m.CreatedAt
            }).ToList()
        };
    }
}
