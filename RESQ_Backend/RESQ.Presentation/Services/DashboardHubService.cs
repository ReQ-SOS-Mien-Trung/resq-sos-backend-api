using MediatR;
using Microsoft.AspNetCore.SignalR;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public class DashboardHubService(
    IHubContext<DashboardHub> hubContext,
    IMediator mediator
) : IDashboardHubService
{
    private const string GroupName = "admin_dashboard";
    private readonly IHubContext<DashboardHub> _hubContext = hubContext;
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc/>
    public async Task PushVictimsByPeriodAsync(CancellationToken cancellationToken = default)
    {
        // Push default view: 6 tháng gần nhất, group by month
        var data = await _mediator.Send(
            new GetVictimsByPeriodQuery(null, null, null, null),
            cancellationToken);

        await _hubContext.Clients.Group(GroupName)
            .SendAsync("ReceiveVictimsByPeriod", data, cancellationToken);
    }
}
