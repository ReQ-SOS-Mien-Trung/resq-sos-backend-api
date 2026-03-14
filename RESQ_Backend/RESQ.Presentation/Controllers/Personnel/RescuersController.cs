using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("personnel/rescuers")]
[ApiController]
public class RescuersController(IMediator mediator) : ControllerBase
{
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
}
