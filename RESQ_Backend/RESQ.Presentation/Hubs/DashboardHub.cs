using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

namespace RESQ.Presentation.Hubs;

/// <summary>
/// Hub SignalR cho dashboard admin.
/// - Admin kết nối → tự động join group "admin_dashboard" và nhận dữ liệu ban đầu.
/// - Server push "ReceiveVictimsByPeriod" khi có SOS mới tạo.
/// - Client có thể gọi GetVictimsByPeriod(from, to, granularity, statuses) để refresh on-demand.
/// 
/// Kết nối: /hubs/dashboard?access_token={jwt}
/// </summary>
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class DashboardHub(IMediator mediator) : Hub
{
    private const string GroupName = "admin_dashboard";
    private readonly IMediator _mediator = mediator;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);

        // Push dữ liệu ban đầu (6 tháng gần nhất, nhóm theo tháng) cho client vừa kết nối
        var data = await _mediator.Send(new GetVictimsByPeriodQuery(null, null, null));
        await Clients.Caller.SendAsync("ReceiveVictimsByPeriod", data);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client gọi để lấy lại dữ liệu chart với tuỳ chọn filter.
    /// </summary>
    /// <param name="from">ISO 8601 string (có thể null → 6 tháng trước).</param>
    /// <param name="to">ISO 8601 string (có thể null → hôm nay).</param>
    /// <param name="granularity">"day" | "month" (null → "month").</param>
    public async Task GetVictimsByPeriod(
        string? from,
        string? to,
        string? granularity)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var f) ? f : null;
        DateTime? toDate = DateTime.TryParse(to, out var t) ? t : null;

        var data = await _mediator.Send(
            new GetVictimsByPeriodQuery(fromDate, toDate, granularity));

        await Clients.Caller.SendAsync("ReceiveVictimsByPeriod", data);
    }
}
