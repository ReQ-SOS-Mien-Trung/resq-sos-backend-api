using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("api/inventory")]
[ApiController]
public class InventoryImportController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportReliefItemsRequest request)
    {
        var command = new ImportReliefItemsCommand
        {
            OrganizationId = request.OrganizationId,
            Items = request.Items
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}