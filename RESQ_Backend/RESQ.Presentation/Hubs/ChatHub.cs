using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.UseCases.Operations.Commands.CoordinatorJoinConversation;
using RESQ.Application.UseCases.Operations.Commands.CoordinatorLeaveConversation;
using RESQ.Application.UseCases.Operations.Commands.SendMessage;

namespace RESQ.Presentation.Hubs;

/// <summary>
/// Hub chat thời gian thực.
/// - Mỗi Victim có 1 phòng chat riêng (không gắn với mission).
/// - AI gợi ý chủ đề → Victim chọn SOS Request → Coordinator join và hỗ trợ.
/// Client kết nối với JWT Bearer token (qua query string ?access_token=...).
/// </summary>
[Authorize(Policy = PermissionConstants.ConversationSelfView)]
public class ChatHub(IMediator mediator, IConversationRepository conversationRepository) : Hub
{
    private readonly IMediator _mediator = mediator;
    private readonly IConversationRepository _conversationRepository = conversationRepository;

    // ─────────────────────────────────────────────────────────────────────────
    // Join / Leave
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Client (Victim hoặc Coordinator) join vào group SignalR của conversation.
    /// Coordinator chỉ được join sau khi đã gọi API CoordinatorJoinConversation.
    /// </summary>
    [Authorize(Policy = PermissionConstants.ConversationSelfView)]
    public async Task JoinConversation(int conversationId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Không xác định được người dùng.");
            return;
        }

        var isParticipant = await _conversationRepository.IsParticipantAsync(conversationId, userId);
        if (!isParticipant)
        {
            await Clients.Caller.SendAsync("Error", "Bạn không phải là thành viên của conversation này.");
            return;
        }

        var groupName = GetGroupName(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedConversation",
            new { conversationId, message = "Đã tham gia cuộc trò chuyện." });
    }

    /// <summary>Client rời phòng chat.</summary>
    [Authorize(Policy = PermissionConstants.ConversationSelfManage)]
    public async Task LeaveConversation(int conversationId)
    {
        var groupName = GetGroupName(conversationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("LeftConversation", new { conversationId });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Messaging
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gửi tin nhắn text trong conversation.
    /// Chỉ participant hợp lệ mới gửi được.
    /// </summary>
    [Authorize(Policy = PermissionConstants.ConversationSelfManage)]
    public async Task SendMessage(int conversationId, string content)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Không xác định được người dùng.");
            return;
        }

        try
        {
            var command = new SendMessageCommand(conversationId, userId, content);
            var result = await _mediator.Send(command);

            var groupName = GetGroupName(conversationId);
            await Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                result.Id,
                result.ConversationId,
                result.SenderId,
                result.SenderName,
                result.Content,
                MessageType = result.MessageType.ToString(),
                result.CreatedAt
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Coordinator join (real-time notification)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Coordinator gọi để tham gia hỗ trợ Victim qua SignalR.
    /// Phải đã được xác thực qua REST API CoordinatorJoinConversation trước,
    /// nhưng method này có thể kết hợp cả hai bước để tiện hơn.
    /// </summary>
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task CoordinatorJoin(int conversationId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Không xác định được người dùng.");
            return;
        }

        try
        {
            var command = new CoordinatorJoinConversationCommand(conversationId, userId);
            var result = await _mediator.Send(command);

            // Add coordinator to SignalR group
            var groupName = GetGroupName(conversationId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // Broadcast system message to the room
            await Clients.Group(groupName).SendAsync("CoordinatorJoined", new
            {
                result.ConversationId,
                result.CoordinatorId,
                Status = result.Status.ToString(),
                result.SystemMessage
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Coordinator gọi để rời phòng chat, chuyển conversation về WaitingCoordinator.
    /// </summary>
    [Authorize(Policy = PermissionConstants.ConversationCoordinatorManage)]
    public async Task CoordinatorLeave(int conversationId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Không xác định được người dùng.");
            return;
        }

        try
        {
            var command = new CoordinatorLeaveConversationCommand(conversationId, userId);
            var result = await _mediator.Send(command);

            // Remove coordinator from SignalR group
            var groupName = GetGroupName(conversationId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            // Broadcast to remaining participants
            await Clients.Group(groupName).SendAsync("CoordinatorLeft", new
            {
                result.ConversationId,
                result.CoordinatorId,
                Status = result.Status.ToString(),
                result.SystemMessage
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static string GetGroupName(int conversationId) => $"conversation_{conversationId}";
}

