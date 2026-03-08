using RESQ.Application.Services;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetOrCreateVictimConversation;

public class GetOrCreateVictimConversationResponse
{
    public int ConversationId { get; set; }
    public Guid? VictimId { get; set; }
    public ConversationStatus Status { get; set; }
    public string? SelectedTopic { get; set; }
    public int? LinkedSosRequestId { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>Lời chào và hướng dẫn từ AI.</summary>
    public string? AiGreetingMessage { get; set; }

    /// <summary>Danh sách chủ đề được AI gợi ý.</summary>
    public List<ChatTopicSuggestion> TopicSuggestions { get; set; } = [];

    public List<ParticipantDto> Participants { get; set; } = [];
}

public class ParticipantDto
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Role { get; set; }
    public DateTime? JoinedAt { get; set; }
}
