using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetRescuers;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("personnel/rescuers")]
[ApiController]
public class RescuersController(IMediator mediator) : ControllerBase
{
    /// <summary>Lấy danh sách rescuer đang rảnh (chưa trong đội) có phân trang.</summary>
    [HttpGet("free")]
    public async Task<IActionResult> GetFreeRescuers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? firstName = null,
        [FromQuery] string? lastName = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? email = null,
        [FromQuery] RescuerType? rescuerType = null)
    {
        var result = await mediator.Send(new GetFreeRescuersQuery(pageNumber, pageSize, firstName, lastName, phone, email, rescuerType));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách rescuer với các filter tuỳ chọn:
    /// có assembly point hay chưa, rescuerType, có team hay chưa,
    /// ability subgroup, ability category.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRescuers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? hasAssemblyPoint = null,
        [FromQuery] bool? hasTeam = null,
        [FromQuery] RescuerType? rescuerType = null,
        [FromQuery] string? abilitySubgroupCode = null,
        [FromQuery] string? abilityCategoryCode = null,
        [FromQuery] string? firstName = null,
        [FromQuery] string? lastName = null,
        [FromQuery] string? email = null)
    {
        var query = new GetRescuersQuery(
            pageNumber,
            pageSize,
            hasAssemblyPoint,
            hasTeam,
            rescuerType,
            abilitySubgroupCode,
            abilityCategoryCode,
            firstName,
            lastName,
            email);

        var result = await mediator.Send(query);
        return Ok(result);
    }
}

