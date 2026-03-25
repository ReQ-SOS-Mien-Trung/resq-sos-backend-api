using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.UpdateItemModel;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/item-model")]
[ApiController]
public class ItemModelsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>Cập nhật item model.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemModelCommand command)
    {
        command.Id = id;
        await _sender.Send(command);
        return NoContent();
    }
}
