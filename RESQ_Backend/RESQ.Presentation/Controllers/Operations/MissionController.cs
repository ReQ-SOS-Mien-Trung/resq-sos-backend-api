using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Application.UseCases.Operations.Queries.GetMissionActivities;
using RESQ.Application.UseCases.Operations.Queries.GetMissionById;
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

    /// <summary>
    /// Coordinator tạo mission mới kèm danh sách activities cho một cluster.
    /// Tự động tạo conversation giữa coordinator và các victim trong cluster.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "1,2,4")]
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
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> GetMissions([FromQuery] int? clusterId)
    {
        var result = await _mediator.Send(new GetMissionsQuery(clusterId));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một mission kèm toàn bộ activities.
    /// </summary>
    [HttpGet("{missionId:int}")]
    [Authorize(Roles = "1,2,4")]
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
    [Authorize(Roles = "1,2,4")]
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
    [Authorize(Roles = "1,2,4")]
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
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> GetMissionActivities([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionActivitiesQuery(missionId));
        return Ok(result);
    }

    /// <summary>
    /// Thêm một activity vào mission đã có.
    /// </summary>
    [HttpPost("{missionId:int}/activities")]
    [Authorize(Roles = "1,2,4")]
    public async Task<IActionResult> AddMissionActivity([FromRoute] int missionId, [FromBody] AddMissionActivityRequestDto dto)
    {
        var command = new AddMissionActivityCommand(
            missionId,
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
    /// Cập nhật nội dung một activity.
    /// </summary>
    [HttpPut("{missionId:int}/activities/{activityId:int}")]
    [Authorize(Roles = "1,2,4")]
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
    [Authorize(Roles = "1,2,4")]
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

    /// <summary>
    /// Lấy tuyến đường từ vị trí của rescuer đến địa điểm đích của một activity.
    /// Rescuer truyền vào tọa độ hiện tại (originLat, originLng) và nhận lại
    /// toàn bộ thông tin tuyến đường (khoảng cách, thời gian, polyline, bước chỉ đường).
    /// vehicle: car | bike | taxi | hd (mặc định: car)
    /// </summary>
    [HttpGet("{missionId:int}/activities/{activityId:int}/route")]
    [Authorize(Roles = "1,2,3,4")]
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
}
