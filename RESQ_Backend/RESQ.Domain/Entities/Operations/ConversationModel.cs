using RESQ.Domain.Enum.Operations;

namespace RESQ.Domain.Entities.Operations;

public class ConversationModel
{
    public int Id { get; set; }

    /// <summary>Victim là chủ phòng chat. Mỗi victim có đúng 1 conversation.</summary>
    public Guid? VictimId { get; set; }

    /// <summary>Mission được gán sau khi coordinator xác nhận hỗ trợ (optional).</summary>
    public int? MissionId { get; set; }

    /// <summary>Trạng thái hiện tại của cuộc hỗ trợ.</summary>
    public ConversationStatus Status { get; set; } = ConversationStatus.AiAssist;

    /// <summary>Chủ đề victim đã chọn (ví dụ: SosRequestSupport, GeneralHelp).</summary>
    public string? SelectedTopic { get; set; }

    /// <summary>SOS Request được victim chọn để hỗ trợ.</summary>
    public int? LinkedSosRequestId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<ConversationParticipantModel> Participants { get; set; } = [];
    public List<MessageModel> Messages { get; set; } = [];
}
