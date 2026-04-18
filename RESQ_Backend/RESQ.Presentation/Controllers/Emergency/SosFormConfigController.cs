using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Presentation.Controllers.Emergency;

[Route("emergency/sos-form-config")]
[ApiController]
[Authorize(Policy = PermissionConstants.SosRequestCreate)]
public class SosFormConfigController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Trả về active SOS rule config để mobile/web victim dựng form động và cache offline.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetSosPriorityRuleConfigQuery());
        return Ok(result);
    }
}
