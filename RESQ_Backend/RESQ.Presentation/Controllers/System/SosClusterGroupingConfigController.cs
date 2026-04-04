using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosClusterGroupingConfig;

namespace RESQ.Presentation.Controllers.System;

[Route("system/sos-cluster-grouping-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class SosClusterGroupingConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy cấu hình khoảng cách tối đa để gom SOS requests vào cùng một cluster.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetSosClusterGroupingConfigQuery());
        return Ok(result);
    }

    /// <summary>Cập nhật khoảng cách tối đa để gom SOS requests vào cùng một cluster.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertSosClusterGroupingConfigRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new UpsertSosClusterGroupingConfigCommand
        {
            UserId = adminId,
            MaximumDistanceKm = request.MaximumDistanceKm
        });

        return Ok(result);
    }
}