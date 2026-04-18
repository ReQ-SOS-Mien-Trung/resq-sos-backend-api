using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Queries.AbilitySubgroupMetadata;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/ability-subgroups")]
[ApiController]
public class AbilitySubgroupsController(IMediator mediator) : ControllerBase
{
    /// <summary>[Metadata] Danh sách ability subgroup dùng cho dropdown (key = code, value = name).</summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        var result = await mediator.Send(new GetAbilitySubgroupMetadataQuery());
        return Ok(result);
    }
}
