using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;
using RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Presentation.Controllers.Operations;

[Route("operations/team-incidents")]
[ApiController]
public class TeamIncidentController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Đội cứu hộ báo cáo sự cố trong quá trình thực hiện nhiệm vụ.
    /// Trạng thái ban đầu: Reported. Tự động cập nhật trạng thái đội → Stuck.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> ReportIncident([FromBody] ReportTeamIncidentRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var command = new ReportTeamIncidentCommand(
            dto.MissionTeamId, dto.Description, dto.Latitude, dto.Longitude, userId);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật trạng thái sự cố.
    /// - Reported → Acknowledged: Coordinator xác nhận
    /// - Acknowledged + NeedsAssistance=true → InProgress; false → Closed
    /// - InProgress → Resolved: sự cố được giải quyết
    /// - Resolved → Closed: Coordinator xác nhận đóng
    /// </summary>
    [HttpPatch("{incidentId:int}/status")]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> UpdateIncidentStatus([FromRoute] int incidentId, [FromBody] UpdateTeamIncidentStatusRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        if (!Enum.TryParse<TeamIncidentStatus>(dto.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { message = $"Trạng thái không hợp lệ: {dto.Status}" });

        var command = new UpdateTeamIncidentStatusCommand(incidentId, newStatus, dto.NeedsAssistance, dto.HasInjuredMember, userId);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách sự cố của một mission.
    /// </summary>
    [HttpGet("by-mission/{missionId:int}")]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> GetIncidentsByMission([FromRoute] int missionId)
    {
        var result = await _mediator.Send(new GetTeamIncidentsQuery(missionId));
        return Ok(result);
    }
}
