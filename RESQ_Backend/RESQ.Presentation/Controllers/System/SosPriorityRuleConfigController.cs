using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;
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

    /// <summary>Cập nhật cấu hình quy tắc ưu tiên SOS (Admin).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSosPriorityRuleConfigRequestDto dto)
    {
        var command = new UpdateSosPriorityRuleConfigCommand(id, dto);

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
