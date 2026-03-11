namespace RESQ.Domain.Entities.Operations;

public class ConversationParticipantModel
{
    public int Id { get; set; }
    public int? ConversationId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? RoleInConversation { get; set; }
    public DateTime? JoinedAt { get; set; }
}
