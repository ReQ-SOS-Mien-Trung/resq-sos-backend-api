using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.CreateItemCategory;
using RESQ.Application.UseCases.Logistics.Commands.DeleteItemCategory;
using RESQ.Application.UseCases.Logistics.Commands.UpdateItemCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetAllItemCategoriesList;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategories;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryByCode;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryById;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryCodes;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/item-category")]
[ApiController]
public class ItemCategoriesController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet]
    public async Task<ActionResult<GetItemCategoriesResponse>> GetAllPaged([FromQuery] GetItemCategoriesQuery query)
    {
        var result = await _sender.Send(query);
        return Ok(result);
    }

    [HttpGet("all")]
    public async Task<ActionResult<List<ItemCategoryDto>>> GetAll()
    {
        var result = await _sender.Send(new GetAllItemCategoriesListQuery());
        return Ok(result);
    }

    [HttpGet("codes")]
    public async Task<ActionResult<List<ItemCategoryCodeDto>>> GetCodes()
    {
        var result = await _sender.Send(new GetItemCategoryCodesQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ItemCategoryDto>> GetById(int id)
    {
        var result = await _sender.Send(new GetItemCategoryByIdQuery(id));
        return Ok(result);
    }

    [HttpGet("code/{code}")]
    public async Task<ActionResult<ItemCategoryDto>> GetByCode(ItemCategoryCode code)
    {
        var result = await _sender.Send(new GetItemCategoryByCodeQuery(code));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateItemCategoryResponse>> Create([FromBody] CreateItemCategoryCommand command)
    {
        var result = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemCategoryCommand command)
    {
        command.Id = id;
        await _sender.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _sender.Send(new DeleteItemCategoryCommand(id));
        return NoContent();
    }
}
