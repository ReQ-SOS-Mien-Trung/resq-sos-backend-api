namespace RESQ.Application.UseCases.Operations.Queries.GetConversationByMission;

public class GetConversationByMissionResponse
{
    public int MissionId { get; set; }

    /// <summary>
    /// Coordinator thấy N conversations (1 per victim).
    /// Victim chỉ thấy 1 conversation của mình.
    /// </summary>
    public List<ConversationItemDto> Conversations { get; set; } = [];
}

public class ConversationItemDto
{
    public int ConversationId { get; set; }
    public ParticipantDto? Coordinator { get; set; }
    public ParticipantDto? Victim { get; set; }
}

public class ParticipantDto
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? RoleInConversation { get; set; }
    public DateTime? JoinedAt { get; set; }
}
