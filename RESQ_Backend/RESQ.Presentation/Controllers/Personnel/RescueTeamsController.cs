using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;
using RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamDetail;
using RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("personnel/rescue-teams")]
[ApiController]
public class RescueTeamsController(IMediator mediator) : ControllerBase
{
    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetAllTeams([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await mediator.Send(new GetAllRescueTeamsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await mediator.Send(new GetRescueTeamDetailQuery(id));
        return Ok(result);
    }

    [HttpPost()]
    [Authorize(Roles = "2")] // Coordinator only
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequestDto request)
    {
        var managedBy = GetCurrentUserId();
        if (managedBy == null) return Unauthorized();

        var id = await mediator.Send(new CreateRescueTeamCommand(
            request.Name, 
            request.Type, 
            request.AssemblyPointId, 
            managedBy.Value, 
            request.MaxMembers, 
            request.Members
        ));
        return Ok(new { Id = id });
    }

    [HttpGet("metadata/status")]
    public async Task<IActionResult> GetStatusMetadata()
    {
        var result = await mediator.Send(new GetRescueTeamStatusMetadataQuery());
        return Ok(result);
    }

    [HttpGet("metadata/types")]
    public async Task<IActionResult> GetTypeMetadata()
    {
        var result = await mediator.Send(new GetRescueTeamTypeMetadataQuery());
        return Ok(result);
    }

    [HttpGet("metadata/member-status")]
    public async Task<IActionResult> GetMemberStatusMetadata()
    {
        var result = await mediator.Send(new GetTeamMemberStatusMetadataQuery());
        return Ok(result);
    }

    [HttpPatch("{id}/schedule-assembly")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> ScheduleAssembly(int id, [FromBody] DateTime assemblyDate)
    {
        await mediator.Send(new ScheduleAssemblyCommand(id, assemblyDate));
        return NoContent();
    }

    [HttpPost("{id}/members")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequestDto request)
    {
        await mediator.Send(new AddTeamMemberCommand(id, request.UserId, request.IsLeader));
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> RemoveMember(int id, Guid userId)
    {
        await mediator.Send(new RemoveTeamMemberCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/members/accept")]
    [Authorize] 
    public async Task<IActionResult> AcceptInvitation(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await mediator.Send(new AcceptInvitationCommand(id, userId.Value));
        return NoContent();
    }

    [HttpPost("{id}/members/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineInvitation(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await mediator.Send(new DeclineInvitationCommand(id, userId.Value));
        return NoContent();
    }

    [HttpPost("{id}/members/check-in")]
    [Authorize]
    public async Task<IActionResult> CheckInMember(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await mediator.Send(new CheckInMemberCommand(id, userId.Value));
        return NoContent();
    }

    [HttpPost("{id}/assign-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> AssignMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Assign"));
        return NoContent();
    }

    [HttpPost("{id}/cancel-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> CancelMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Cancel"));
        return NoContent();
    }

    [HttpPost("{id}/start-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> StartMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Start"));
        return NoContent();
    }

    [HttpPost("{id}/finish-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> FinishMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Finish"));
        return NoContent();
    }

    [HttpPost("{id}/report-incident")]
    [Authorize] // Usually the Team Leader (Rescuer) reports this
    public async Task<IActionResult> ReportIncident(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "ReportIncident"));
        return NoContent();
    }

    [HttpPost("{id}/resolve-incident")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> ResolveIncident(int id, [FromQuery] bool hasInjuredMember)
    {
        await mediator.Send(new ResolveIncidentCommand(id, hasInjuredMember));
        return NoContent();
    }

    [HttpPost("{id}/set-unavailable")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> SetUnavailable(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "SetUnavailable"));
        return NoContent();
    }

    [HttpPost("{id}/disband")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> DisbandTeam(int id)
    {
        await mediator.Send(new DisbandTeamCommand(id));
        return NoContent();
    }
}
