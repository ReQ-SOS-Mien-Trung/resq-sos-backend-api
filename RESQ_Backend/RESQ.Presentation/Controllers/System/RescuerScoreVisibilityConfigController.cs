using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescuerScoreVisibilityConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerScoreVisibilityConfig;

namespace RESQ.Presentation.Controllers.System;

[Route("system/rescuer-score-visibility-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class RescuerScoreVisibilityConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy cấu hình ngưỡng tối thiểu để hiển thị điểm rescuer.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetRescuerScoreVisibilityConfigQuery());
        return Ok(result);
    }

    /// <summary>Cập nhật ngưỡng tối thiểu số lần đánh giá để hiển thị điểm rescuer toàn hệ thống.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertRescuerScoreVisibilityConfigRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");

        var result = await _mediator.Send(new UpsertRescuerScoreVisibilityConfigCommand
        {
            UserId = adminId,
            MinimumEvaluationCount = request.MinimumEvaluationCount
        });

        return Ok(result);
    }
}