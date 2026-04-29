using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Maintenance.Commands.SyncSeedData;

namespace RESQ.Presentation.Controllers.Admin;

[Route("admin/maintenance")]
[ApiController]
//[Authorize(Policy = PermissionConstants.SystemConfigManage)]
public sealed class MaintenanceController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost("sync-seed-data")]
    [ProducesResponseType(typeof(SeedDataSyncReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncSeedData([FromQuery] bool dryRun = false)
    {
        var result = await _mediator.Send(new SyncSeedDataCommand(dryRun));
        return Ok(result);
    }
}
