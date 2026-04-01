using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Personnel.Queries.GetAllRescueTeams;
using RESQ.Application.UseCases.Personnel.Queries.GetMyRescueTeam;
using RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamDetail;
using RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamsByCluster;
using RESQ.Application.UseCases.Personnel.Queries.RescueTeamMetadata;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;
using RESQ.Application.UseCases.Personnel.Commands.SetTeamAvailable;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("personnel/rescue-teams")]
[ApiController]
public class RescueTeamsController(IMediator mediator) : ControllerBase
{
    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        return userId;
    }

    /// <summary>Lấy danh sách tất cả đội cứu hộ có phân trang.</summary>
    [HttpGet("")]
    [Authorize(Policy = PermissionConstants.PolicyPersonnelAccess)]
    public async Task<IActionResult> GetAllTeams([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await mediator.Send(new GetAllRescueTeamsQuery(pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>Xem chi tiết đội cứu hộ theo ID.</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = PermissionConstants.PolicyPersonnelAccess)]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await mediator.Send(new GetRescueTeamDetailQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách đội cứu hộ sắp xếp theo khoảng cách gần nhất so với cluster SOS.
    /// Khoảng cách được tính từ toạ độ điểm tập kết của mỗi đội tới trung tâm cluster.
    /// </summary>
    [HttpGet("by-cluster/{clusterId}")]
    [Authorize(Policy = PermissionConstants.PolicyPersonnelAccess)]
    public async Task<IActionResult> GetByCluster(int clusterId)
    {
        var result = await mediator.Send(new GetRescueTeamsByClusterQuery(clusterId));
        return Ok(result);
    }

    /// <summary>Lấy đội cứu hộ hiện tại của user đang đăng nhập.</summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyTeam()
    {
        var userId = GetCurrentUserId();
        var result = await mediator.Send(new GetMyRescueTeamQuery(userId));
        return Ok(result);
    }

    /// <summary>Tạo đội cứu hộ mới từ danh sách rescuer đã check-in.</summary>
    [HttpPost()]
    [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)] // Coordinator_Global | Coordinator_Point
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequestDto request)
    {
        var managedBy = GetCurrentUserId();
        var id = await mediator.Send(new CreateRescueTeamCommand(
            request.Name, 
            request.Type, 
            request.AssemblyPointId,
            request.AssemblyEventId,
            managedBy, 
            request.MaxMembers, 
            request.Members
        ));
        return Ok(new { Id = id });
    }

    /// <summary>[Metadata] Danh sách trạng thái đội cứu hộ.</summary>
    [HttpGet("metadata/status")]
    public async Task<IActionResult> GetStatusMetadata()
    {
        var result = await mediator.Send(new GetRescueTeamStatusMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại đội cứu hộ.</summary>
    [HttpGet("metadata/types")]
    public async Task<IActionResult> GetTypeMetadata()
    {
        var result = await mediator.Send(new GetRescueTeamTypeMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách trạng thái thành viên đội.</summary>
    [HttpGet("metadata/member-status")]
    public async Task<IActionResult> GetMemberStatusMetadata()
    {
        var result = await mediator.Send(new GetTeamMemberStatusMetadataQuery());
        return Ok(result);
    }

    /// <summary>Xóa thành viên khỏi đội cứu hộ.</summary>
    [HttpDelete("{id}/members/{userId}")]
    [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)]
    public async Task<IActionResult> RemoveMember(int id, Guid userId)
    {
        await mediator.Send(new RemoveTeamMemberCommand(id, userId));
        return NoContent();
    }

    /// <summary>Giao nhiệm vụ cho đội (chuyển sang trạng thái Assigned).</summary>
    [HttpPost("{id}/assign-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> AssignMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Assign"));
        return NoContent();
    }

    /// <summary>Huỷ nhiệm vụ của đội.</summary>
    [HttpPost("{id}/cancel-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> CancelMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Cancel"));
        return NoContent();
    }

    /// <summary>Bắt đầu thực hiện nhiệm vụ.</summary>
    [HttpPost("{id}/start-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> StartMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Start"));
        return NoContent();
    }

    /// <summary>Hoàn thành nhiệm vụ.</summary>
    [HttpPost("{id}/finish-mission")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> FinishMission(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "Finish"));
        return NoContent();
    }

    /// <summary>Đánh dấu đội không sẵn sàng nhận nhiệm vụ.</summary>
    [HttpPost("{id}/set-unavailable")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> SetUnavailable(int id)
    {
        await mediator.Send(new ChangeTeamMissionStateCommand(id, "SetUnavailable"));
        return NoContent();
    }

    /// <summary>Giải tán đội cứu hộ.</summary>
    [HttpPost("{id}/disband")]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> DisbandTeam(int id)
    {
        await mediator.Send(new DisbandTeamCommand(id));
        return NoContent();
    }

    /// <summary>Leader xác nhận đội sẵn sàng nhận nhiệm vụ (Gathering → Available).</summary>
    [HttpPost("{id}/set-available")]
    [Authorize]
    public async Task<IActionResult> SetAvailable(int id)
    {
        var userId = GetCurrentUserId();
        await mediator.Send(new SetTeamAvailableCommand(id, userId));
        return NoContent();
    }
}
