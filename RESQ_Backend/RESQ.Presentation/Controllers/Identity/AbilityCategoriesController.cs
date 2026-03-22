using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.CreateAbilityCategory;
using RESQ.Application.UseCases.Identity.Commands.DeleteAbilityCategory;
using RESQ.Application.UseCases.Identity.Commands.UpdateAbilityCategory;
using RESQ.Application.UseCases.Identity.Queries.GetAllAbilityCategories;
using RESQ.Application.UseCases.Identity.Queries.AbilityCategoryMetadata;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/ability-categories")]
[ApiController]
public class AbilityCategoriesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy tất cả danh mục ability.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetAllAbilityCategoriesQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách ability category dùng cho dropdown (key = code, value = name).</summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetCategoryMetadata()
    {
        var result = await _mediator.Send(new GetAbilityCategoryMetadataQuery());
        return Ok(result);
    }

    /// <summary>Tạo danh mục ability mới.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateAbilityCategoryCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), result);
    }

    /// <summary>Cập nhật danh mục ability.</summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAbilityCategoryRequest request)
    {
        var command = new UpdateAbilityCategoryCommand(id, request.Code, request.Description);
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Xóa danh mục ability.</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteAbilityCategoryCommand(id);
        await _mediator.Send(command);
        return NoContent();
    }
}

public record UpdateAbilityCategoryRequest(string Code, string? Description);
