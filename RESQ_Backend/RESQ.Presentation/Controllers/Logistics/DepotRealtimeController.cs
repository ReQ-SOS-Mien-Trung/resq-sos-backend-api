using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/depot-realtime")]
[ApiController]
public class DepotRealtimeController(IDepotRealtimeOutboxAdminService adminService) : ControllerBase
{
    private readonly IDepotRealtimeOutboxAdminService _adminService = adminService;

    /// <summary>
    /// Replay thủ công các event dead-letter cho luồng realtime depot.
    /// </summary>
    [HttpPost("dead-letter/replay")]
    [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
    public async Task<IActionResult> ReplayDeadLetters([FromBody] ReplayDepotDeadLetterRequest request, CancellationToken cancellationToken)
    {
        if (request.EventIds.Count == 0)
            return BadRequest(new { Message = "EventIds is required." });

        var count = await _adminService.ReplayDeadLettersAsync(request.EventIds, cancellationToken);
        return Ok(new { Replayed = count });
    }
}

public sealed class ReplayDepotDeadLetterRequest
{
    public List<Guid> EventIds { get; set; } = [];
}
