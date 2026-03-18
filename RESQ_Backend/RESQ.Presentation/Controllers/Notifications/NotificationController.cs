using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Notifications.Commands.BroadcastAlert;
using RESQ.Application.UseCases.Notifications.Commands.MarkAllNotificationsRead;
using RESQ.Application.UseCases.Notifications.Commands.MarkNotificationRead;
using RESQ.Application.UseCases.Notifications.Queries.GetMyNotifications;

namespace RESQ.Presentation.Controllers.Notifications;

[Route("notifications")]
[ApiController]
[Authorize]
public class NotificationController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy danh sách notifications của user đang đăng nhập (phân trang, mới nhất trước).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new GetMyNotificationsQuery(userId, page, pageSize));
        return Ok(result);
    }

    /// <summary>Đánh dấu một notification đã đọc.</summary>
    [HttpPatch("{userNotificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] int userNotificationId)
    {
        var userId = GetUserId();
        await _mediator.Send(new MarkNotificationReadCommand(userNotificationId, userId));
        return NoContent();
    }

    /// <summary>Đánh dấu tất cả notifications của user là đã đọc.</summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await _mediator.Send(new MarkAllNotificationsReadCommand(userId));
        return NoContent();
    }

    /// <summary>
    /// [Admin] Gửi broadcast alert (ví dụ: cảnh báo lũ) đến toàn bộ user active trong hệ thống.
    /// </summary>
    [HttpPost("broadcast")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    public async Task<IActionResult> BroadcastAlert([FromBody] BroadcastAlertRequestDto dto)
    {
        var userId = GetUserId();
        var command = new BroadcastAlertCommand(userId, dto.Title, dto.Body, dto.Type ?? "flood_alert");
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var id))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        return id;
    }
}

public record BroadcastAlertRequestDto(string Title, string Body, string? Type);
