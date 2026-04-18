using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.LinkSosRequestToConversation;

public class LinkSosRequestToConversationResponse
{
    public int ConversationId { get; set; }
    public int LinkedSosRequestId { get; set; }
    public ConversationStatus Status { get; set; }
    public string AiConfirmationMessage { get; set; } = string.Empty;
}
