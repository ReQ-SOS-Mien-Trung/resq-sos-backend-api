using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetCheckInRadiusConfig;

namespace RESQ.Presentation.Controllers.System;

[Route("system/check-in-radius-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class CheckInRadiusConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy cấu hình bán kính check-in tại điểm tập kết.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetCheckInRadiusConfigQuery());
        return Ok(result);
    }

    /// <summary>Cập nhật bán kính check-in tại điểm tập kết (mặc định 200m).</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertCheckInRadiusConfigRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new UpsertCheckInRadiusConfigCommand
        {
            UserId = adminId,
            MaxRadiusMeters = request.MaxRadiusMeters
        });

        return Ok(result);
    }
}
