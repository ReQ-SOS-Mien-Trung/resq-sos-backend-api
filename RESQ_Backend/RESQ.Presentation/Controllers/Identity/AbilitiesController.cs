using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;
using RESQ.Application.UseCases.Identity.Queries.GetAllAbilities;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerAbilities;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/abilities")]
[ApiController]
public class AbilitiesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Xem danh sách tất cả ability dùng cho rescuer chọn.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAbilities()
    {
        var query = new GetAllAbilitiesQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Lưu danh sách ability đã chọn của rescuer hiện tại.</summary>
    [HttpPost("rescuer")]
    [Authorize(Policy = PermissionConstants.IdentityProfileUpdate)]
    public async Task<IActionResult> SaveRescuerAbilities([FromBody] SaveRescuerAbilitiesRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Không thể xác định người dùng.");
        }

        var abilities = dto.Abilities.Select(a => new RescuerAbilityItem
        {
            AbilityId = a.AbilityId,
            Level = a.Level
        }).ToList();

        var command = new SaveRescuerAbilitiesCommand(userId, abilities);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Xem danh sách ability của rescuer hiện tại.</summary>
    [HttpGet("rescuer/me")]
    [Authorize(Policy = PermissionConstants.IdentitySelfView)]
    public async Task<IActionResult> GetMyAbilities()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("Không thể xác định người dùng.");
        }

        var query = new GetRescuerAbilitiesQuery(userId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
