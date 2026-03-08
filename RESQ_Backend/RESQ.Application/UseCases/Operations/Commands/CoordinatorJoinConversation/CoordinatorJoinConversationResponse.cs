using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;

public class CoordinatorJoinConversationResponse
{
    public int ConversationId { get; set; }
    public Guid CoordinatorId { get; set; }
    public ConversationStatus Status { get; set; }
    public string SystemMessage { get; set; } = string.Empty;
}
