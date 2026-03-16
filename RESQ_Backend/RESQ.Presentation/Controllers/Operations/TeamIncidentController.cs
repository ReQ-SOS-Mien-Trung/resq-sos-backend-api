using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Operations.Commands.ReportTeamIncident;
using RESQ.Application.UseCases.Operations.Commands.UpdateTeamIncidentStatus;
using RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;
using RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Presentation.Controllers.Operations;

[Route("operations/team-incidents")]
[ApiController]
public class TeamIncidentController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lấy danh sách tất cả sự cố.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "1,2,3")]
    public async Task<IActionResult> GetAllIncidents()
    {
        var result = await _mediator.Send(new GetAllTeamIncidentsQuery());
        return Ok(result);
    }

    /// <summary>Đội cứu hộ báo cáo sự cố trong khi thực hiện nhiệm vụ.</summary>
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

    /// <summary>Cập nhật trạng thái sự cố (Reported → Acknowledged → InProgress → Resolved → Closed).</summary>
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
