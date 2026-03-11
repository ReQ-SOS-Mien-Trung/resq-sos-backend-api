using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Queries.GetOrCreateVictimConversation;

public class GetOrCreateVictimConversationQueryHandler(
    IConversationRepository conversationRepository,
    IChatSupportAiService chatSupportAiService,
    ILogger<GetOrCreateVictimConversationQueryHandler> logger
) : IRequestHandler<GetOrCreateVictimConversationQuery, GetOrCreateVictimConversationResponse>
{
    public async Task<GetOrCreateVictimConversationResponse> Handle(
        GetOrCreateVictimConversationQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetOrCreateForVictimAsync(
            request.VictimId, cancellationToken);

        logger.LogInformation(
            "GetOrCreateVictimConversation: ConversationId={Id} Status={Status} for Victim={VictimId}",
            conversation.Id, conversation.Status, request.VictimId);

        // Lấy gợi ý chủ đề AI (luôn trả về để client có thể hiển thị quick-pick)
        var suggestions = await chatSupportAiService.GetTopicSuggestionsAsync(
            request.VictimId, cancellationToken);

        return new GetOrCreateVictimConversationResponse
        {
            ConversationId = conversation.Id,
            VictimId = conversation.VictimId,
            Status = conversation.Status,
            SelectedTopic = conversation.SelectedTopic,
            LinkedSosRequestId = conversation.LinkedSosRequestId,
            CreatedAt = conversation.CreatedAt,
            AiGreetingMessage = suggestions.IsSuccess ? suggestions.AiGreetingMessage : null,
            TopicSuggestions = suggestions.IsSuccess ? suggestions.Topics : [],
            Participants = conversation.Participants.Select(p => new ParticipantDto
            {
                UserId = p.UserId,
                UserName = p.UserName,
                Role = p.RoleInConversation,
                JoinedAt = p.JoinedAt
            }).ToList()
        };
    }
}
