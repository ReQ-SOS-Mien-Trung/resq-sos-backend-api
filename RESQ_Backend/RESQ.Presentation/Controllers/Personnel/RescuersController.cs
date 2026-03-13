using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;

namespace RESQ.Presentation.Controllers.Personnel;

[Route("personnel/rescuers")]
[ApiController]
public class RescuersController(IMediator mediator) : ControllerBase
{
    [HttpGet("free")]
    public async Task<IActionResult> GetFreeRescuers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await mediator.Send(new GetFreeRescuersQuery(pageNumber, pageSize));
        return Ok(result);
    }
}