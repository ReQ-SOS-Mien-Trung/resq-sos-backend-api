using MediatR;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetVictimConversations;

public class GetVictimConversationsQueryHandler(
    IConversationRepository conversationRepository
) : IRequestHandler<GetVictimConversationsQuery, List<VictimConversationDto>>
{
    public async Task<List<VictimConversationDto>> Handle(
        GetVictimConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = await conversationRepository
            .GetVictimConversationsAsync(request.VictimId, cancellationToken);

        return conversations.Select(c => new VictimConversationDto
        {
            ConversationId = c.Id,
            Status = c.Status,
            SelectedTopic = c.SelectedTopic,
            LinkedSosRequestId = c.LinkedSosRequestId,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }
}
