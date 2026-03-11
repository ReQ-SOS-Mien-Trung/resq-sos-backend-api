using MediatR;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SendMessage;

public record SendMessageCommand(
    int ConversationId,
    Guid SenderId,
    string Content,
    MessageType MessageType = MessageType.UserMessage
) : IRequest<SendMessageResponse>;
