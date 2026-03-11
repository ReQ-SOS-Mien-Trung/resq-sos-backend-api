using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("api/rescue-teams")]
[ApiController]
public class RescueTeamsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "2")] // Chỉ cho phép Role ID = 2 (Coordinator) tạo đội cứu hộ
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequestDto request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var managedBy))
        {
            return Unauthorized(new { Message = "Không xác định được danh tính người điều phối từ token." });
        }

        var id = await mediator.Send(new CreateRescueTeamCommand(
            request.Name, 
            request.Type, 
            request.AssemblyPointId, 
            managedBy, 
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
    public async Task<IActionResult> ScheduleAssembly(int id, [FromBody] DateTime assemblyDate)
    {
        await mediator.Send(new ScheduleAssemblyCommand(id, assemblyDate));
        return NoContent();
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequestDto request)
    {
        await mediator.Send(new AddTeamMemberCommand(id, request.UserId, request.IsLeader));
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, Guid userId)
    {
        await mediator.Send(new RemoveTeamMemberCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/members/{userId}/accept")]
    public async Task<IActionResult> AcceptInvitation(int id, Guid userId)
    {
        await mediator.Send(new AcceptInvitationCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/members/{userId}/decline")]
    public async Task<IActionResult> DeclineInvitation(int id, Guid userId)
    {
        await mediator.Send(new DeclineInvitationCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/members/{userId}/check-in")]
    public async Task<IActionResult> CheckInMember(int id, Guid userId)
    {
        await mediator.Send(new CheckInMemberCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/assign-mission")]
    public async Task<IActionResult> AssignMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Assign"));
        return NoContent();
    }

    [HttpPost("{id}/cancel-mission")]
    public async Task<IActionResult> CancelMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Cancel"));
        return NoContent();
    }

    [HttpPost("{id}/start-mission")]
    public async Task<IActionResult> StartMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Start"));
        return NoContent();
    }

    [HttpPost("{id}/finish-mission")]
    public async Task<IActionResult> FinishMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Finish"));
        return NoContent();
    }

    [HttpPost("{id}/report-incident")]
    public async Task<IActionResult> ReportIncident(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "ReportIncident"));
        return NoContent();
    }

    [HttpPost("{id}/resolve-incident")]
    public async Task<IActionResult> ResolveIncident(int id, [FromQuery] bool hasInjuredMember)
    {
        await mediator.Send(new ResolveIncidentCommand(id, hasInjuredMember));
        return NoContent();
    }

    [HttpPost("{id}/set-unavailable")]
    public async Task<IActionResult> SetUnavailable(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "SetUnavailable"));
        return NoContent();
    }

    [HttpPost("{id}/disband")]
    public async Task<IActionResult> DisbandTeam(int id)
    {
        await mediator.Send(new DisbandTeamCommand(id));
        return NoContent();
    }
}
