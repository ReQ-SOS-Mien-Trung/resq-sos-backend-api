using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationMessages;

public class GetConversationMessagesResponse
{
    public int ConversationId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<MessageDto> Messages { get; set; } = [];
}

public class MessageDto
{
    public int Id { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }
    public MessageType MessageType { get; set; }
    public DateTime? CreatedAt { get; set; }
}
