using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SendMessage;

public class SendMessageResponse
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }
    public MessageType MessageType { get; set; }
    public DateTime? CreatedAt { get; set; }
}
