using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToMission;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Application.UseCases.Operations.Commands.UnassignTeamFromMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeams;
using RESQ.Application.UseCases.Operations.Queries.GetMyTeamMissions;
using RESQ.Application.UseCases.Operations.Queries.GetMissions;
using RESQ.Application.UseCases.Operations.Queries.GetRescuerRoute;

namespace RESQ.Presentation.Controllers.Operations;

[Route("operations/missions")]
[ApiController]
public class MissionController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

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
            return Unauthorized();

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
            return Unauthorized();

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
        if (result is null) return NotFound();
        return Ok(result);
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
        var command = new UpdateMissionStatusCommand(missionId, dto.Status);
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

    /// <summary>Thêm activity vào mission (tuỳ chọn giao đội ngay bằng RescueTeamId).</summary>
    [HttpPost("{missionId:int}/activities")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> AddMissionActivity([FromRoute] int missionId, [FromBody] AddMissionActivityRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new AddMissionActivityCommand(
            missionId,
            dto.Step,
            dto.ActivityCode,
            dto.ActivityType,
            dto.Description,
            dto.Target,
            dto.Items,
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
            dto.ActivityCode,
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
            return Unauthorized();

        var command = new UpdateActivityStatusCommand(activityId, dto.Status, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ============================================================
    // ROUTING (GOONG MAP)
    // ============================================================

    /// <summary>Lấy tuyến đường từ vị trí rescuer đến đích activity (vehicle: car|bike|taxi|hd).</summary>
    [HttpGet("{missionId:int}/activities/{activityId:int}/route")]
    [Authorize(Policy = PermissionConstants.PolicyRouteAccess)]
    public async Task<IActionResult> GetRescuerRoute(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromQuery] double originLat,
        [FromQuery] double originLng,
        [FromQuery] string vehicle = "car")
    {
        var result = await _mediator.Send(new GetRescuerRouteQuery(activityId, originLat, originLng, vehicle));
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
            return Unauthorized();

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
            return Unauthorized();

        var command = new UnassignTeamFromMissionCommand(missionTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
