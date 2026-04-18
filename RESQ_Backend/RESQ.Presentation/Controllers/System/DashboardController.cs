using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

namespace RESQ.Presentation.Controllers.System;

/// <summary>
/// Dashboard analytics cấp hệ thống — dành cho admin/coordinator.
/// </summary>
[ApiController]
[Route("dashboard")]
[Authorize(Policy = PermissionConstants.SystemUserView)]
public class DashboardController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lấy biến động số victim theo khoảng thời gian (bar chart data).
    /// </summary>
    /// <param name="from">Ngày bắt đầu (inclusive). Mặc định: 6 tháng trước.</param>
    /// <param name="to">Ngày kết thúc (inclusive). Mặc định: hôm nay.</param>
    /// <param name="granularity">"day" | "month". Mặc định: "month".</param>
    [HttpGet("victims-by-period")]
    public async Task<IActionResult> GetVictimsByPeriod(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? granularity)
    {
        var result = await _mediator.Send(
            new GetVictimsByPeriodQuery(from, to, granularity));

        return Ok(result);
    }
}

