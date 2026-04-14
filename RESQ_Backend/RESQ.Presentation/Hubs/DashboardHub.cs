using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

namespace RESQ.Presentation.Hubs;

/// <summary>
/// Hub SignalR cho dashboard admin.
/// - Admin k?t n?i ? t? d?ng join group "admin_dashboard" và nh?n d? li?u ban d?u.
/// - Server push "ReceiveVictimsByPeriod" khi có SOS m?i t?o.
/// - Client có th? g?i GetVictimsByPeriod(from, to, granularity, statuses) d? refresh on-demand.
/// 
/// K?t n?i: /hubs/dashboard?access_token={jwt}
/// </summary>
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class DashboardHub(IMediator mediator) : Hub
{
    private const string GroupName = "admin_dashboard";
    private readonly IMediator _mediator = mediator;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);

        // Push d? li?u ban d?u (6 tháng g?n nh?t, nhóm theo tháng) cho client v?a k?t n?i
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
    /// Client g?i d? l?y l?i d? li?u chart v?i tu? ch?n filter.
    /// </summary>
    /// <param name="from">ISO 8601 string (có th? null ? 6 tháng tru?c).</param>
    /// <param name="to">ISO 8601 string (có th? null ? hôm nay).</param>
    /// <param name="granularity">"day" | "month" (null ? "month").</param>
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
