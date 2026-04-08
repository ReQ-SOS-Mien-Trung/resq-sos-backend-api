using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;
using RESQ.Application.UseCases.Operations.Commands.CoordinatorLeaveConversation;
using RESQ.Application.UseCases.Operations.Commands.LinkSosRequestToConversation;
using RESQ.Application.UseCases.Operations.Commands.SelectSupportTopic;
using RESQ.Application.UseCases.Operations.Queries.GetConversationByMission;
using RESQ.Application.UseCases.Operations.Queries.GetConversationMessages;
using RESQ.Application.UseCases.Operations.Queries.GetConversationsWaiting;
using RESQ.Application.UseCases.Operations.Queries.GetOrCreateVictimConversation;
using RESQ.Application.UseCases.Operations.Queries.GetVictimConversations;

namespace RESQ.Presentation.Controllers.Operations;

/// <summary>Quản lý chat hỗ trợ giữa Victim, AI và Coordinator. Real-time qua SignalR tại /hubs/chat.</summary>
[Route("operations/conversations")]
[ApiController]
public class ConversationController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    // ─── Victim ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Victim mở màn hình chat → lấy hoặc tạo phòng chat của mình.
    /// Trả về thông tin conversation và gợi ý chủ đề từ AI.
    /// </summary>
    [HttpGet("my-conversation")]
    [Authorize(Policy = PermissionConstants.ConversationSelfManage)]
    public async Task<IActionResult> GetOrCreateMyConversation()
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new GetOrCreateVictimConversationQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Victim xem lịch sử tất cả các cuộc hội thoại của mình.
    /// Mỗi lần chọn chủ đề mới sẽ tạo một conversation riêng biệt.
    /// </summary>
    [HttpGet("my-conversations")]
    [Authorize(Policy = PermissionConstants.ConversationSelfView)]
    public async Task<IActionResult> GetMyConversations()
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new GetVictimConversationsQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Victim chọn chủ đề hỗ trợ (ví dụ: SosRequestSupport, SupplyRequest...).
    /// AI sẽ phản hồi và nếu topic = SosRequestSupport, trả về danh sách SOS của victim.
    /// Sau khi chọn topic, conversation này chuyển sang WaitingCoordinator;
    /// gọi lại /my-conversation sẽ tạo một conversation MỚI cho chủ đề tiếp theo.
    /// </summary>
    [HttpPost("{conversationId:int}/select-topic")]
    [Authorize(Policy = PermissionConstants.ConversationSelfManage)]
    public async Task<IActionResult> SelectTopic(
        [FromRoute] int conversationId,
        [FromBody] SelectTopicRequest dto)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(
            new SelectSupportTopicCommand(conversationId, userId, dto.TopicKey));
        return Ok(result);
    }

    /// <summary>
    /// Victim chọn SOS Request cụ thể cần được hỗ trợ.
    /// Sau bước này, conversation chuyển sang WaitingCoordinator,
    /// Coordinator có thể thấy trong danh sách chờ và join.
    /// </summary>
    [HttpPost("{conversationId:int}/link-sos-request")]
    [Authorize(Policy = PermissionConstants.ConversationSelfManage)]
    public async Task<IActionResult> LinkSosRequest(
        [FromRoute] int conversationId,
        [FromBody] LinkSosRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(
            new LinkSosRequestToConversationCommand(conversationId, userId, dto.SosRequestId));
        return Ok(result);
    }

    // ─── Coordinator ──────────────────────────────────────────────────────────

    /// <summary>
    /// Coordinator xem danh sách phòng chat đang chờ hỗ trợ.
    /// </summary>
    [HttpGet("waiting")]
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task<IActionResult> GetWaitingConversations()
    {
        var result = await _mediator.Send(new GetConversationsWaitingQuery());
        return Ok(result);
    }

    /// <summary>
    /// Coordinator tham gia hỗ trợ Victim trong một conversation.
    /// Sau khi gọi, Coordinator trở thành participant và có thể join SignalR group.
    /// </summary>
    [HttpPost("{conversationId:int}/join")]
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task<IActionResult> CoordinatorJoin([FromRoute] int conversationId)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(
            new CoordinatorJoinConversationCommand(conversationId, userId));
        return Ok(result);
    }

    /// <summary>
    /// Coordinator rời khỏi phòng chat.
    /// Conversation chuyển về WaitingCoordinator để coordinator khác có thể tiếp nhận.
    /// </summary>
    [HttpPost("{conversationId:int}/leave")]
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task<IActionResult> CoordinatorLeave([FromRoute] int conversationId)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(
            new CoordinatorLeaveConversationCommand(conversationId, userId));
        return Ok(result);
    }

    // ─── Shared ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy lịch sử tin nhắn của một conversation (phân trang, cũ nhất trước).
    /// </summary>
    [HttpGet("{conversationId:int}/messages")]
    [Authorize(Policy = PermissionConstants.ConversationSelfView)]
    public async Task<IActionResult> GetMessages(
        [FromRoute] int conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(
            new GetConversationMessagesQuery(conversationId, userId, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Legacy: lấy conversations theo missionId (cho màn hình quản lý mission).
    /// </summary>
    [HttpGet("mission/{missionId:int}")]
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task<IActionResult> GetConversationByMission([FromRoute] int missionId)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new GetConversationByMissionQuery(missionId, userId));
        return Ok(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var id))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        return id;
    }
}

public record SelectTopicRequest(string TopicKey);
public record LinkSosRequestDto(int SosRequestId);

