using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationMessages;

public record GetConversationMessagesQuery(
    int ConversationId,
    Guid RequesterId,
    int Page = 1,
    int PageSize = 50
) : IRequest<GetConversationMessagesResponse>;
