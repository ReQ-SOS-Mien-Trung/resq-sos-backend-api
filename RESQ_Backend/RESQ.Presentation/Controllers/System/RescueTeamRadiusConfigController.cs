using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescueTeamRadiusConfig;

namespace RESQ.Presentation.Controllers.System;

[Route("system/rescue-team-radius-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class RescueTeamRadiusConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy cấu hình bán kính tìm kiếm đội cứu hộ theo cluster.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetRescueTeamRadiusConfigQuery());
        return Ok(result);
    }

    /// <summary>Cập nhật bán kính tìm kiếm đội cứu hộ theo cluster.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertRescueTeamRadiusConfigRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new UpsertRescueTeamRadiusConfigCommand
        {
            UserId = adminId,
            MaxRadiusKm = request.MaxRadiusKm
        });

        return Ok(result);
    }
}
