using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionSuccessRateSummary;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuersDailyStatistics;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosRequestsSummary;

namespace RESQ.Presentation.Controllers.Personnel;

/// <summary>
/// Dashboard analytics liên quan đến nhân sự, đội cứu hộ và hoạt động nhiệm vụ — dành cho admin/coordinator.
/// </summary>
[ApiController]
[Route("personnel/dashboard")]
[Authorize(Policy = PermissionConstants.SystemUserView)]
public class PersonnelDashboardController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// [Dashboard – Thẻ tóm tắt] Thống kê rescuer hôm nay so với hôm qua.
    /// </summary>
    [HttpGet("rescuers/daily-statistics")]
    public async Task<IActionResult> GetRescuersDailyStatistics()
    {
        var result = await _mediator.Send(new GetRescuersDailyStatisticsQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard – Thẻ tóm tắt] Tỷ lệ hoàn thành mission (Completed / tổng finished) hôm nay so với hôm qua.
    /// </summary>
    [HttpGet("missions/success-rate/summary")]
    public async Task<IActionResult> GetMissionSuccessRateSummary()
    {
        var result = await _mediator.Send(new GetMissionSuccessRateSummaryQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard – Thẻ tóm tắt] Tổng số SOS request hôm nay so với hôm qua.
    /// </summary>
    [HttpGet("sos-requests/summary")]
    public async Task<IActionResult> GetSosRequestsSummary()
    {
        var result = await _mediator.Send(new GetSosRequestsSummaryQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard – Bảng đội] Danh sách tất cả đội cứu hộ có phân trang, ưu tiên đội có thay đổi mới nhất.
    /// </summary>
    [HttpGet("rescue-teams")]
    public async Task<IActionResult> GetRescueTeams(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetAdminTeamListQuery(pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard – Chi tiết đội] Toàn bộ thông tin của một đội: thành viên, lịch sử missions + activities, tỉ lệ hoàn thành.
    /// </summary>
    [HttpGet("rescue-teams/{id:int}")]
    public async Task<IActionResult> GetRescueTeamDetail(int id)
    {
        var result = await _mediator.Send(new GetAdminTeamDetailQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard – Chi tiết rescuer] Điểm theo từng mission, overall score, avg per-criteria và lịch sử tham gia đội.
    /// </summary>
    [HttpGet("rescuers/{rescuerId:guid}/scores")]
    public async Task<IActionResult> GetRescuerScores(Guid rescuerId)
    {
        var result = await _mediator.Send(new GetRescuerMissionScoresQuery(rescuerId));
        return Ok(result);
    }
}
