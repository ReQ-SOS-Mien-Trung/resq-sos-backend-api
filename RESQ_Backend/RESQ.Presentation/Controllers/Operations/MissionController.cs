using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Application.UseCases.Operations.Commands.CompleteMissionTeamExecution;
using RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;
using RESQ.Application.UseCases.Operations.Commands.SaveMissionTeamReportDraft;
using RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;
using RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
using RESQ.Application.UseCases.Operations.Queries.GetMyTeamActivities;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamRoute;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeams;
using RESQ.Application.UseCases.Operations.Queries.MissionMetadata;
using RESQ.Application.UseCases.Operations.Queries.GetMyTeamMissions;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

namespace RESQ.Presentation.Controllers.Operations;

[Route("operations/missions")]
[ApiController]
public class MissionController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Metadata] Danh sách trạng thái mission.</summary>
    [HttpGet("metadata/statuses")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMissionStatusesMetadata()
    {
        var result = await _mediator.Send(new GetMissionStatusesMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách trạng thái mission activity.</summary>
    [HttpGet("metadata/activity-statuses")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMissionActivityStatusesMetadata()
    {
        var result = await _mediator.Send(new GetMissionActivityStatusesMetadataQuery());
        return Ok(result);
    }

    // ============================================================
    // MISSIONS
    // ============================================================

    /// <summary>Coordinator tạo mission mới kèm danh sách activities cho một cluster.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> CreateMission([FromBody] CreateMissionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new CreateMissionCommand(
            dto.ClusterId,
            dto.MissionType,
            dto.PriorityScore,
            dto.StartTime,
            dto.ExpectedEndTime,
            dto.Activities,
            userId
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách tất cả missions, có thể filter theo clusterId.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissions([FromQuery] int? clusterId)
    {
        var result = await _mediator.Send(new GetMissionsQuery(clusterId));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách missions mà đội của user hiện tại đang được giao.
    /// </summary>
    [HttpGet("my-team")]
    [Authorize]
    public async Task<IActionResult> GetMyTeamMissions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new GetMyTeamMissionsQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một mission kèm toàn bộ activities.
    /// </summary>
    [HttpGet("{missionId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissionById([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionByIdQuery(missionId));
        return Ok(result ?? throw new NotFoundException($"Không tìm thấy mission #{missionId}."));
    }

    /// <summary>
    /// Cập nhật thông tin chung của mission (type, priority, thời gian).
    /// </summary>
    [HttpPut("{missionId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> UpdateMission([FromRoute] int missionId, [FromBody] UpdateMissionRequestDto dto)
    {
        var command = new UpdateMissionCommand(
            missionId,
            dto.MissionType,
            dto.PriorityScore,
            dto.StartTime,
            dto.ExpectedEndTime
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật trạng thái mission: pending | in_progress | completed | cancelled.
    /// </summary>
    [HttpPatch("{missionId:int}/status")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)] // Global | Point | TeamUpdate
    public async Task<IActionResult> UpdateMissionStatus([FromRoute] int missionId, [FromBody] UpdateMissionStatusRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new UpdateMissionStatusCommand(missionId, dto.Status, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Đội cứu hộ báo sự cố cho toàn bộ phần mission của chính missionTeam này.</summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/incident")]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> ReportMissionTeamIncident(
        [FromRoute] int missionId,
        [FromRoute] int missionTeamId,
        [FromBody] ReportMissionTeamIncidentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new ReportMissionTeamIncidentCommand(
            missionId,
            missionTeamId,
            dto.Description,
            dto.Latitude,
            dto.Longitude,
            dto.NeedsRescueAssistance,
            dto.AssistanceSos,
            userId);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ============================================================
    // ACTIVITIES
    // ============================================================

    /// <summary>
    /// Lấy danh sách activities của một mission.
    /// </summary>
    [HttpGet("{missionId:int}/activities")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)]
    public async Task<IActionResult> GetMissionActivities([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionActivitiesQuery(missionId));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách activities được giao cho đội của user hiện tại trong một mission.
    /// </summary>
    [HttpGet("{missionId:int}/activities/my-team")]
    [Authorize]
    public async Task<IActionResult> GetMyTeamActivities([FromRoute] int missionId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new GetMyTeamActivitiesQuery(missionId, userId));
        return Ok(result);
    }

    /// <summary>Thêm activity vào mission (tuỳ chọn giao đội ngay bằng RescueTeamId).</summary>
    [HttpPost("{missionId:int}/activities")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> AddMissionActivity([FromRoute] int missionId, [FromBody] AddMissionActivityRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new AddMissionActivityCommand(
            missionId,
            dto.Step,
            dto.ActivityType,
            dto.Description,
            dto.Priority,
            dto.EstimatedTime,
            dto.SosRequestId,
            dto.DepotId,
            dto.DepotName,
            dto.DepotAddress,
            dto.SuppliesToCollect,
            dto.Target,
            dto.TargetLatitude,
            dto.TargetLongitude,
            dto.RescueTeamId,
            userId
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật nội dung một activity.
    /// </summary>
    [HttpPut("{missionId:int}/activities/{activityId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> UpdateMissionActivity([FromRoute] int missionId, [FromRoute] int activityId, [FromBody] UpdateMissionActivityRequestDto dto)
    {
        var command = new UpdateMissionActivityCommand(
            activityId,
            dto.Step,
            dto.ActivityType,
            dto.Description,
            dto.Target,
            dto.Items,
            dto.TargetLatitude,
            dto.TargetLongitude
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật trạng thái activity: pending | in_progress | completed | cancelled | skipped.
    /// </summary>
    [HttpPatch("{missionId:int}/activities/{activityId:int}/status")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)] // includes ActivityTeamManage | ActivityOwnManage
    public async Task<IActionResult> UpdateActivityStatus([FromRoute] int missionId, [FromRoute] int activityId, [FromBody] UpdateActivityStatusRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new UpdateActivityStatusCommand(activityId, dto.Status, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Đội cứu hộ báo sự cố cho một activity cụ thể; activity đó sẽ fail và activity kế tiếp của cùng team có thể auto-start.</summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/incident")]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> ReportMissionActivityIncident(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromBody] ReportMissionActivityIncidentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new ReportMissionActivityIncidentCommand(
            missionId,
            activityId,
            dto.Description,
            dto.Latitude,
            dto.Longitude,
            userId);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Depot manager xác nhận đã nhận lại vật tư từ đội cứu hộ (RETURN_SUPPLIES: PendingConfirmation → Succeed + restock kho).
    /// </summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/confirm-return")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> ConfirmReturnSupplies(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromBody] ConfirmReturnSuppliesRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new ConfirmReturnSuppliesCommand(
            activityId,
            missionId,
            userId,
            dto.ConsumableItems,
            dto.ReusableItems,
            dto.DiscrepancyNote);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Giao một rescue team (đã hoặc chưa assigned vào mission) để thực hiện một activity cụ thể.</summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/team")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> AssignTeamToActivity([FromRoute] int missionId, [FromRoute] int activityId, [FromBody] AssignTeamToActivityRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new AssignTeamToActivityCommand(activityId, missionId, dto.RescueTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ============================================================
    // ROUTING (GOONG MAP)
    // ============================================================

    /// <summary>Lấy tuyến đường từ vị trí rescuer đến đích activity (vehicle: car|bike|taxi|hd).</summary>
    /// <remarks>
    /// API này vẫn có thể trả HTTP 200 nếu request hợp lệ nhưng Goong không tạo được tuyến đường.
    /// Frontend phải kiểm tra trường <c>status</c> trong response body trước khi dùng dữ liệu tuyến đường.
    /// Chỉ sử dụng <c>route</c> khi <c>status</c> là <c>OK</c>; nếu không, đọc <c>errorMessage</c> để xử lý lỗi.
    /// </remarks>
    [HttpGet("{missionId:int}/activities/{activityId:int}/route")]
    [Authorize(Policy = PermissionConstants.PolicyRouteAccess)]
    [ProducesResponseType(typeof(GetRescuerRouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRescuerRoute(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromQuery] double originLat,
        [FromQuery] double originLng,
        [FromQuery] string vehicle = "car")
    {
        var result = await _mediator.Send(new GetRescuerRouteQuery(missionId, activityId, originLat, originLng, vehicle));
        return Ok(result);
    }

    /// <summary>Lấy tuyến đường toàn bộ mission của một team, bao gồm tất cả điểm cần tới theo thứ tự activity.</summary>
    /// <remarks>
    /// Nếu truyền <c>originLat</c>/<c>originLng</c>, API tính route từ vị trí được chỉ định như hành vi cũ.
    /// Nếu bỏ trống cả hai, API tự lấy vị trí snapshot hiện tại của team (hoặc điểm tập kết nếu chưa có current location)
    /// để frontend có thể theo dõi team trực tiếp trên Goong map.
    /// </remarks>
    [HttpGet("{missionId:int}/teams/{missionTeamId:int}/route")]
    [Authorize(Policy = PermissionConstants.PolicyRouteAccess)]
    [ProducesResponseType(typeof(GetMissionTeamRouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMissionTeamRoute(
        [FromRoute] int missionId,
        [FromRoute] int missionTeamId,
        [FromQuery] double? originLat = null,
        [FromQuery] double? originLng = null,
        [FromQuery] string vehicle = "car")
    {
        var result = await _mediator.Send(
            new GetMissionTeamRouteQuery(missionId, missionTeamId, originLat, originLng, vehicle));
        return Ok(result);
    }

    // ============================================================
    // TEAM ASSIGNMENTS
    // ============================================================

    /// <summary>
    /// Lấy danh sách đội cứu hộ được giao cho một mission.
    /// </summary>
    [HttpGet("{missionId:int}/teams")]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissionTeams([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionTeamsQuery(missionId));
        return Ok(result);
    }

    /// <summary>Giao một đội cứu hộ (trạng thái Available) vào mission.</summary>
    [HttpPost("{missionId:int}/teams")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> AssignTeamToMission([FromRoute] int missionId, [FromBody] AssignTeamToMissionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new AssignTeamToMissionCommand(missionId, dto.RescueTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Gỡ một đội cứu hộ khỏi mission (chỉ khi đội chưa bắt đầu thực thi).
    /// </summary>
    [HttpDelete("{missionId:int}/teams/{missionTeamId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> UnassignTeamFromMission([FromRoute] int missionId, [FromRoute] int missionTeamId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new UnassignTeamFromMissionCommand(missionTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đánh dấu đội đã hoàn tất phần thực thi ngoài hiện trường và chuyển sang chờ nộp báo cáo.
    /// </summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/complete-execution")]
    [Authorize]
    public async Task<IActionResult> CompleteMissionTeamExecution([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] CompleteMissionTeamExecutionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new CompleteMissionTeamExecutionCommand(missionId, missionTeamId, userId, dto.Note);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy báo cáo hiện tại của một mission team, bao gồm draft nếu có.
    /// </summary>
    [HttpGet("{missionId:int}/teams/{missionTeamId:int}/report")]
    [Authorize]
    public async Task<IActionResult> GetMissionTeamReport([FromRoute] int missionId, [FromRoute] int missionTeamId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new GetMissionTeamReportQuery(missionId, missionTeamId, userId));
        return Ok(result);
    }

    /// <summary>
    /// Lưu nháp báo cáo cho một mission team.
    /// </summary>
    [HttpPut("{missionId:int}/teams/{missionTeamId:int}/report-draft")]
    [Authorize]
    public async Task<IActionResult> SaveMissionTeamReportDraft([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] SaveMissionTeamReportDraftRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new SaveMissionTeamReportDraftCommand(
            missionId,
            missionTeamId,
            userId,
            dto.TeamSummary,
            dto.TeamNote,
            dto.IssuesJson,
            dto.ResultJson,
            dto.EvidenceJson,
            dto.Activities,
            dto.MemberEvaluations);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Nộp báo cáo cuối cùng cho một mission team. Chỉ đội trưởng được phép thực hiện.
    /// </summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/report-submit")]
    [Authorize]
    public async Task<IActionResult> SubmitMissionTeamReport([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] SubmitMissionTeamReportRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var command = new SubmitMissionTeamReportCommand(
            missionId,
            missionTeamId,
            userId,
            dto.TeamSummary,
            dto.TeamNote,
            dto.IssuesJson,
            dto.ResultJson,
            dto.EvidenceJson,
            dto.Activities,
            dto.MemberEvaluations);

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
