using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetOrCreateVictimConversation;

/// <summary>
/// Victim mở màn hình chat → lấy hoặc tạo phòng chat của mình.
/// Trả về thông tin conversation kèm gợi ý chủ đề AI nếu phòng mới.
/// </summary>
public record GetOrCreateVictimConversationQuery(Guid VictimId)
    : IRequest<GetOrCreateVictimConversationResponse>;
