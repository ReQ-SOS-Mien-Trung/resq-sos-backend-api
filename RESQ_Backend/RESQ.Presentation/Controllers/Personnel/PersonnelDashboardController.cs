using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionSuccessRateSummary;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportDashboardSummary;
using RESQ.Application.UseCases.SystemConfig.Queries.GetMissionTeamReportsDashboard;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerOverview;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuersDailyStatistics;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosRequestsSummary;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;
using RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

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
    /// [Dashboard – Cứu hộ viên] Thống kê tổng quan rescuer đã được duyệt theo 12 tháng gần nhất.
    /// </summary>
    /// <remarks>
    /// Sample response:
    ///
    ///     {
    ///       "generatedAt": "2026-04-28T07:00:00Z",
    ///       "timezone": "Asia/Ho_Chi_Minh",
    ///       "totals": {
    ///         "total": 20,
    ///         "core": 5,
    ///         "volunteer": 15,
    ///         "active": 18,
    ///         "banned": 2
    ///       },
    ///       "thisMonth": {
    ///         "month": 4,
    ///         "year": 2026,
    ///         "newCount": 15,
    ///         "previousNewCount": 5,
    ///         "growthPercent": 200
    ///       },
    ///       "peakMonth": {
    ///         "month": 4,
    ///         "year": 2026,
    ///         "monthLabel": "Th4",
    ///         "newCount": 15
    ///       },
    ///       "monthly": [
    ///         {
    ///           "month": 5,
    ///           "year": 2025,
    ///           "monthLabel": "Th5",
    ///           "total": 0,
    ///           "newCount": 0,
    ///           "core": 0,
    ///           "volunteer": 0
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    [HttpGet("rescuers/overview")]
    [ProducesResponseType(typeof(RescuerOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRescuerOverview([FromQuery] int months = 12)
    {
        var result = await _mediator.Send(new GetRescuerOverviewQuery(months));
        return Ok(result);
    }

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
        [FromQuery] int pageSize = 10,
        [FromQuery] RescueTeamType? teamType = null,
        [FromQuery] RescueTeamStatus? status = null,
        [FromQuery] string? assemblyPointName = null,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAdminTeamListQuery(
            pageNumber,
            pageSize,
            teamType,
            status,
            assemblyPointName,
            search));
        return Ok(result);
    }

    /// <summary>[Dashboard - Metadata] Danh sách loại đội cứu hộ dùng cho filter teamType.</summary>
    [HttpGet("rescue-teams/metadata/team-types")]
    public async Task<IActionResult> GetRescueTeamTypeMetadata()
    {
        var result = await _mediator.Send(new GetRescueTeamTypeMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Dashboard - Metadata] Danh sách trạng thái đội cứu hộ dùng cho filter status.</summary>
    [HttpGet("rescue-teams/metadata/statuses")]
    public async Task<IActionResult> GetRescueTeamStatusMetadata()
    {
        var result = await _mediator.Send(new GetRescueTeamStatusMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Dashboard - Metadata] Danh sách tên điểm tập kết dùng cho filter assemblyPointName.</summary>
    [HttpGet("rescue-teams/metadata/assembly-point-names")]
    public async Task<IActionResult> GetRescueTeamAssemblyPointNameMetadata()
    {
        var result = await _mediator.Send(new GetAssemblyPointNameMetadataQuery());
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
    /// [Dashboard - Thẻ báo cáo] Tổng quan trạng thái báo cáo mission team sau khi hoàn tất thực thi.
    /// </summary>
    /// <param name="reportStatuses">Danh sách trạng thái báo cáo cần lọc: NotStarted, Draft, Submitted.</param>
    [HttpGet("mission-team-reports/summary")]
    public async Task<IActionResult> GetMissionTeamReportsSummary(
        [FromQuery] List<MissionTeamReportStatus>? reportStatuses = null)
    {
        var result = await _mediator.Send(new GetMissionTeamReportDashboardSummaryQuery(reportStatuses));
        return Ok(result);
    }

    /// <summary>
    /// [Dashboard - Danh sách báo cáo] Danh sách báo cáo mission team sau khi hoàn tất thực thi.
    /// </summary>
    [HttpGet("mission-team-reports")]
    public async Task<IActionResult> GetMissionTeamReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? reportStatus = null,
        [FromQuery] int? teamId = null,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetMissionTeamReportsDashboardQuery(
            pageNumber,
            pageSize,
            reportStatus,
            teamId,
            search));

        return Ok(result);
    }

    /// <summary>
    /// [Dashboard - Chi tiết rescuer] Điểm theo từng mission, overall score, avg per-criteria và lịch sử tham gia đội.
    /// </summary>
    [HttpGet("rescuers/{rescuerId:guid}/scores")]
    public async Task<IActionResult> GetRescuerScores(Guid rescuerId)
    {
        var result = await _mediator.Send(new GetRescuerMissionScoresQuery(rescuerId));
        return Ok(result);
    }
}
