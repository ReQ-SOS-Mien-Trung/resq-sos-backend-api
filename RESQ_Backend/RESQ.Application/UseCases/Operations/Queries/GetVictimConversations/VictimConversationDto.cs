using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetVictimConversations;

public class VictimConversationDto
{
    public int ConversationId { get; set; }
    public ConversationStatus Status { get; set; }
    public string? SelectedTopic { get; set; }
    public int? LinkedSosRequestId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
