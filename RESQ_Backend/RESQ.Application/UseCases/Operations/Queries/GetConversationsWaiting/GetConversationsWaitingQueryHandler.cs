using MediatR;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationsWaiting;

public class GetConversationsWaitingQueryHandler(
    IConversationRepository conversationRepository
) : IRequestHandler<GetConversationsWaitingQuery, List<ConversationWaitingDto>>
{
    public async Task<List<ConversationWaitingDto>> Handle(
        GetConversationsWaitingQuery request,
        CancellationToken cancellationToken)
    {
        var conversations = await conversationRepository
            .GetConversationsWaitingForCoordinatorAsync(cancellationToken);

        return conversations.Select(c =>
        {
            var victim = c.Participants.FirstOrDefault(p => p.RoleInConversation == "Victim");
            return new ConversationWaitingDto
            {
                ConversationId = c.Id,
                VictimId = c.VictimId,
                VictimName = victim?.UserName,
                SelectedTopic = c.SelectedTopic,
                LinkedSosRequestId = c.LinkedSosRequestId,
                UpdatedAt = c.UpdatedAt
            };
        }).ToList();
    }
}
