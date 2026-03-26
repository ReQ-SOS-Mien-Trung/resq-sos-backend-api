using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Application.UseCases.SystemConfig.Queries.SosStatusMetadata;

namespace RESQ.Presentation.Controllers.System;

/// <summary>
/// Dashboard analytics endpoints cho admin.
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
    /// <param name="statuses">
    /// Danh sách SOS status cần lọc (có thể gửi nhiều giá trị, e.g. ?statuses=Pending&amp;statuses=Closed).
    /// Để trống = lấy tất cả.
    /// </param>
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

    // /// <summary>
    // /// [Metadata] Danh sách trạng thái SOS dùng cho dropdown.
    // /// </summary>
    // [HttpGet("metadata/sos-statuses")]
    // public async Task<IActionResult> GetSosStatusMetadata()
    // {
    //     var result = await _mediator.Send(new GetSosStatusMetadataQuery());
    //     return Ok(result);
    // }
}
