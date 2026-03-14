using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetVictimConversations;

public record GetVictimConversationsQuery(Guid VictimId)
    : IRequest<List<VictimConversationDto>>;
