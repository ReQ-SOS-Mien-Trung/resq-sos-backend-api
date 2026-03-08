using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationsWaiting;

/// <summary>
/// Coordinator lấy danh sách phòng chat đang chờ hỗ trợ (WaitingCoordinator).
/// </summary>
public record GetConversationsWaitingQuery() : IRequest<List<ConversationWaitingDto>>;

public class ConversationWaitingDto
{
    public int ConversationId { get; set; }
    public Guid? VictimId { get; set; }
    public string? VictimName { get; set; }
    public string? SelectedTopic { get; set; }
    public int? LinkedSosRequestId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
