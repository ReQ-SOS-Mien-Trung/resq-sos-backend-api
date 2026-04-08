using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.SystemConfig.Commands.ActivateSosPriorityRuleConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.CreateSosPriorityRuleConfigDraft;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;
using RESQ.Application.UseCases.SystemConfig.Commands.ValidateSosPriorityRuleConfig;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Presentation.Controllers.System;

[Route("system/sos-priority-rule-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public class SosPriorityRuleConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy cấu hình quy tắc ưu tiên SOS.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetSosPriorityRuleConfigQuery());
        return Ok(result);
    }

    /// <summary>Lấy chi tiết cấu hình quy tắc ưu tiên SOS theo ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetSosPriorityRuleConfigByIdQuery(id));
        return Ok(result);
    }

    /// <summary>Liệt kê tất cả version config rulebase SOS.</summary>
    [HttpGet("versions")]
    public async Task<IActionResult> GetVersions()
    {
        var result = await _mediator.Send(new GetSosPriorityRuleConfigVersionsQuery());
        return Ok(result);
    }

    /// <summary>Clone active config hiện tại thành draft mới.</summary>
    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft()
    {
        var result = await _mediator.Send(new CreateSosPriorityRuleConfigDraftCommand(GetCurrentUserId()));
        return Ok(result);
    }

    /// <summary>Cập nhật draft config SOS.</summary>
    [HttpPut("drafts/{id:int}")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSosPriorityRuleConfigRequestDto dto)
    {
        var command = new UpdateSosPriorityRuleConfigCommand(id, dto);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Validate config candidate và preview trên SOS request thật nếu có sos_request_id.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateSosPriorityRuleConfigRequestDto dto)
    {
        var command = new ValidateSosPriorityRuleConfigCommand(dto.SosRequestId, dto);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Activate một draft config thành version active hiện tại.</summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var result = await _mediator.Send(new ActivateSosPriorityRuleConfigCommand(id, GetCurrentUserId()));
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        }

        return userId;
    }
}
