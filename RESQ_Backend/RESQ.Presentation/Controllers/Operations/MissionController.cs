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
using RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;
using RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;
using RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;
using RESQ.Application.UseCases.Operations.Commands.CreateMission;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionActivityIncident;
using RESQ.Application.UseCases.Operations.Commands.ReportMissionTeamIncident;
using RESQ.Application.UseCases.Operations.Commands.SaveMissionTeamReportDraft;
using RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;
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
using RESQ.Domain.Enum.Operations;

namespace RESQ.Presentation.Controllers.Operations;

[Route("operations/missions")]
[ApiController]
public class MissionController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>[Metadata] Danh sách tr?ng thái mission.</summary>
    [HttpGet("metadata/statuses")]
    [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMissionStatusesMetadata()
    {
        var result = await _mediator.Send(new GetMissionStatusesMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách tr?ng thái mission activity.</summary>
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

    /// <summary>Coordinator t?o mission m?i kèm danh sách activities cho m?t cluster.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> CreateMission([FromBody] CreateMissionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

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
    /// L?y danh sách t?t c? missions, có th? filter theo clusterId.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissions([FromQuery] int? clusterId)
    {
        var result = await _mediator.Send(new GetMissionsQuery(clusterId));
        return Ok(result);
    }

    /// <summary>
    /// L?y danh sách missions mà d?i c?a user hi?n t?i dang du?c giao.
    /// </summary>
    [HttpGet("my-team")]
    [Authorize(Policy = PermissionConstants.MissionSelfView)]
    public async Task<IActionResult> GetMyTeamMissions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var result = await _mediator.Send(new GetMyTeamMissionsQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Xem chi ti?t m?t mission kèm toàn b? activities.
    /// </summary>
    [HttpGet("{missionId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissionById([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionByIdQuery(missionId));
        return Ok(result ?? throw new NotFoundException($"Không tìm th?y mission #{missionId}."));
    }

    /// <summary>
    /// C?p nh?t thông tin chung c?a mission (type, priority, th?i gian).
    /// </summary>
    [HttpPut("{missionId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> UpdateMission([FromRoute] int missionId, [FromBody] UpdateMissionRequestDto dto)
    {
        var activityPatches = dto.Activities ?? [];
        Guid? updatedBy = null;

        if (activityPatches.Count > 0)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

            updatedBy = userId;
        }

        var command = new UpdateMissionCommand(
            missionId,
            dto.MissionType,
            dto.PriorityScore,
            dto.StartTime,
            dto.ExpectedEndTime,
            updatedBy,
            activityPatches.Select(activity => new UpdateMissionActivityPatch(
                activity.ActivityId,
                activity.Step,
                activity.Description,
                activity.Target,
                activity.TargetLatitude,
                activity.TargetLongitude,
                activity.Items,
                activity.AssemblyPointId))
            .ToList()
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// C?p nh?t tr?ng thái mission: pending | in_progress | completed | cancelled.
    /// </summary>
    [HttpPatch("{missionId:int}/status")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)] // Global | Point | TeamUpdate
    public async Task<IActionResult> UpdateMissionStatus([FromRoute] int missionId, [FromBody] UpdateMissionStatusRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new UpdateMissionStatusCommand(missionId, dto.Status, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Ð?i c?u h? báo s? c? cho toàn b? ph?n mission c?a chính missionTeam này.</summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/incident")]
    [Authorize(Policy = PermissionConstants.MissionIncidentReport)]
    public async Task<IActionResult> ReportMissionTeamIncident(
        [FromRoute] int missionId,
        [FromRoute] int missionTeamId,
        [FromBody] ReportMissionTeamIncidentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new ReportMissionTeamIncidentCommand(
            missionId,
            missionTeamId,
            dto,
            userId);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ============================================================
    // ACTIVITIES
    // ============================================================

    /// <summary>
    /// L?y danh sách activities c?a m?t mission.
    /// </summary>
    [HttpGet("{missionId:int}/activities")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)]
    public async Task<IActionResult> GetMissionActivities([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionActivitiesQuery(missionId));
        return Ok(result);
    }

    /// <summary>
    /// L?y danh sách activities du?c giao cho d?i c?a user hi?n t?i trong m?t mission.
    /// </summary>
    [HttpGet("{missionId:int}/activities/my-team")]
    [Authorize(Policy = PermissionConstants.ActivitySelfView)]
    public async Task<IActionResult> GetMyTeamActivities([FromRoute] int missionId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var result = await _mediator.Send(new GetMyTeamActivitiesQuery(missionId, userId));
        return Ok(result);
    }

    /// <summary>Thêm activity vào mission (tu? ch?n giao d?i ngay b?ng RescueTeamId).</summary>
    [HttpPost("{missionId:int}/activities")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> AddMissionActivity([FromRoute] int missionId, [FromBody] AddMissionActivityRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

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
    /// C?p nh?t n?i dung m?t activity.
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
            dto.AssemblyPointId,
            dto.TargetLatitude,
            dto.TargetLongitude
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// C?p nh?t tr?ng thái activity: Planned | OnGoing | Succeed | PendingConfirmation | Failed | Cancelled.
    /// </summary>
    [HttpPatch("{missionId:int}/activities/{activityId:int}/status")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)] // includes ActivityTeamManage | ActivityOwnManage
    public async Task<IActionResult> UpdateActivityStatus([FromRoute] int missionId, [FromRoute] int activityId, [FromBody] UpdateActivityStatusRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var validStatuses = string.Join(", ", Enum.GetNames<MissionActivityStatus>());

        if (string.IsNullOrWhiteSpace(dto.Status)
            || !Enum.TryParse<MissionActivityStatus>(dto.Status.Trim(), ignoreCase: true, out var newStatus)
            || !Enum.IsDefined(newStatus))
        {
            throw new BadRequestException(
                $"Tr?ng thái activity không h?p l?: '{dto.Status}'. Các giá tr? h?p l?: {validStatuses}.");
        }

        var command = new UpdateActivityStatusCommand(missionId, activityId, newStatus, userId, dto.ImageUrl);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Ð?ng b? hàng d?i offline c?p nh?t tr?ng thái activity cho d?i hi?n t?i trên nhi?u mission.</summary>
    [HttpPost("activities/sync/my-team")]
    [Authorize(Policy = PermissionConstants.PolicyActivityExecutionSync)]
    public async Task<IActionResult> SyncMissionActivities([FromBody] SyncMissionActivitiesRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new SyncMissionActivitiesCommand(userId, dto.Items);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Ð?i c?u h? báo activity incident theo contract V2 cho m?t ho?c nhi?u activity thu?c cùng mission team.</summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/activity-incident")]
    [Authorize(Policy = PermissionConstants.MissionIncidentReport)]
    public async Task<IActionResult> ReportMissionActivityIncident(
        [FromRoute] int missionId,
        [FromRoute] int missionTeamId,
        [FromBody] ReportMissionActivityIncidentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new ReportMissionActivityIncidentCommand(
            missionId,
            missionTeamId,
            dto,
            userId);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Team xác nh?n thông tin s? d?ng buffer d? trù khi l?y hàng t?i kho cho m?t COLLECT_SUPPLIES activity.
    /// G?i tru?c khi chuy?n activity sang Succeed n?u có dùng buffer. Không b?t bu?c n?u không dùng buffer.
    /// Vi?c tr? kho th?c t? x?y ra khi activity du?c chuy?n sang Succeed.
    /// </summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/confirm-pickup")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)]
    public async Task<IActionResult> ConfirmMissionSupplyPickup(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromBody] ConfirmMissionSupplyPickupRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new ConfirmMissionSupplyPickupCommand(
            activityId,
            missionId,
            userId,
            dto.BufferUsages);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Depot manager xác nh?n dã nh?n l?i v?t ph?m t? d?i c?u h? (RETURN_SUPPLIES: PendingConfirmation ? Succeed + restock kho).
    /// </summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/confirm-return")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> ConfirmReturnSupplies(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromBody] ConfirmReturnSuppliesRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

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

    /// <summary>
    /// Team xác nh?n dã giao v?t ph?m kèm s? lu?ng th?c t? t?ng m?t hàng (DELIVER_SUPPLIES: OnGoing ? Succeed).
    /// N?u giao thi?u, h? th?ng t? d?ng t?o RETURN_SUPPLIES activity cho s? lu?ng th?a.
    /// </summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/confirm-delivery")]
    [Authorize(Policy = PermissionConstants.PolicyActivityAccess)]
    public async Task<IActionResult> ConfirmDeliverySupplies(
        [FromRoute] int missionId,
        [FromRoute] int activityId,
        [FromBody] ConfirmDeliverySuppliesRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new ConfirmDeliverySuppliesCommand(
            activityId,
            missionId,
            userId,
            dto.ActualDeliveredItems,
            dto.DeliveryNote);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Giao m?t rescue team (dã ho?c chua assigned vào mission) d? th?c hi?n m?t activity c? th?.</summary>
    [HttpPost("{missionId:int}/activities/{activityId:int}/team")]
    [Authorize(Policy = PermissionConstants.PolicyActivityManage)]
    public async Task<IActionResult> AssignTeamToActivity([FromRoute] int missionId, [FromRoute] int activityId, [FromBody] AssignTeamToActivityRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new AssignTeamToActivityCommand(activityId, missionId, dto.RescueTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ============================================================
    // ROUTING (GOONG MAP)
    // ============================================================

    /// <summary>L?y tuy?n du?ng t? v? trí rescuer d?n dích activity (vehicle: car|bike|taxi|hd).</summary>
    /// <remarks>
    /// API này v?n có th? tr? HTTP 200 n?u request h?p l? nhung Goong không t?o du?c tuy?n du?ng.
    /// Frontend ph?i ki?m tra tru?ng <c>status</c> trong response body tru?c khi dùng d? li?u tuy?n du?ng.
    /// Ch? s? d?ng <c>route</c> khi <c>status</c> là <c>OK</c>; n?u không, d?c <c>errorMessage</c> d? x? lý l?i.
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

    /// <summary>L?y tuy?n du?ng toàn b? mission c?a m?t team, bao g?m t?t c? di?m c?n t?i theo th? t? activity.</summary>
    /// <remarks>
    /// N?u truy?n <c>originLat</c>/<c>originLng</c>, API tính route t? v? trí du?c ch? d?nh nhu hành vi cu.
    /// N?u b? tr?ng c? hai, API t? l?y v? trí snapshot hi?n t?i c?a team (ho?c di?m t?p k?t n?u chua có current location)
    /// d? frontend có th? theo dõi team tr?c ti?p trên Goong map.
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
    /// L?y danh sách d?i c?u h? du?c giao cho m?t mission.
    /// </summary>
    [HttpGet("{missionId:int}/teams")]
    [Authorize(Policy = PermissionConstants.PolicyMissionAccess)]
    public async Task<IActionResult> GetMissionTeams([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetMissionTeamsQuery(missionId));
        return Ok(result);
    }

    /// <summary>Giao m?t d?i c?u h? (tr?ng thái Available) vào mission.</summary>
    [HttpPost("{missionId:int}/teams")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> AssignTeamToMission([FromRoute] int missionId, [FromBody] AssignTeamToMissionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new AssignTeamToMissionCommand(missionId, dto.RescueTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// G? m?t d?i c?u h? kh?i mission (ch? khi d?i chua b?t d?u th?c thi).
    /// </summary>
    [HttpDelete("{missionId:int}/teams/{missionTeamId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyMissionManage)]
    public async Task<IActionResult> UnassignTeamFromMission([FromRoute] int missionId, [FromRoute] int missionTeamId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new UnassignTeamFromMissionCommand(missionTeamId, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Ðánh d?u d?i dã hoàn t?t ph?n th?c thi ngoài hi?n tru?ng và chuy?n sang ch? n?p báo cáo.
    /// </summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/complete-execution")]
    [Authorize(Policy = PermissionConstants.MissionExecutionComplete)]
    public async Task<IActionResult> CompleteMissionTeamExecution([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] CompleteMissionTeamExecutionRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var command = new CompleteMissionTeamExecutionCommand(missionId, missionTeamId, userId, dto.Note);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// L?y báo cáo hi?n t?i c?a m?t mission team, bao g?m draft n?u có.
    /// </summary>
    [HttpGet("{missionId:int}/teams/{missionTeamId:int}/report")]
    [Authorize(Policy = PermissionConstants.MissionReportView)]
    public async Task<IActionResult> GetMissionTeamReport([FromRoute] int missionId, [FromRoute] int missionTeamId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

        var result = await _mediator.Send(new GetMissionTeamReportQuery(missionId, missionTeamId, userId));
        return Ok(result);
    }

    /// <summary>
    /// Luu nháp báo cáo cho m?t mission team.
    /// </summary>
    [HttpPut("{missionId:int}/teams/{missionTeamId:int}/report-draft")]
    [Authorize(Policy = PermissionConstants.MissionReportEdit)]
    public async Task<IActionResult> SaveMissionTeamReportDraft([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] SaveMissionTeamReportDraftRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

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
    /// N?p báo cáo cu?i cùng cho m?t mission team. Ch? d?i tru?ng du?c phép th?c hi?n.
    /// </summary>
    [HttpPost("{missionId:int}/teams/{missionTeamId:int}/report-submit")]
    [Authorize(Policy = PermissionConstants.MissionReportSubmit)]
    public async Task<IActionResult> SubmitMissionTeamReport([FromRoute] int missionId, [FromRoute] int missionTeamId, [FromBody] SubmitMissionTeamReportRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedException("Token không h?p l? ho?c không tìm th?y thông tin ngu?i dùng.");

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
