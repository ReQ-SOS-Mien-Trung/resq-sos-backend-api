using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileTypeCategory;
using RESQ.Application.UseCases.Identity.Commands.DeleteDocumentFileTypeCategory;
using RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileTypeCategory;
using RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypeCategories;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/document-file-type-categories")]
[ApiController]
public class DocumentFileTypeCategoriesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy tất cả danh mục loại tài liệu.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetAllDocumentFileTypeCategoriesQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Tạo danh mục loại tài liệu mới.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentFileTypeCategoryCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), result);
    }

    /// <summary>Cập nhật danh mục loại tài liệu.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentFileTypeCategoryRequest request)
    {
        var command = new UpdateDocumentFileTypeCategoryCommand(id, request.Code, request.Description);
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Xóa danh mục loại tài liệu.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteDocumentFileTypeCategoryCommand(id);
        await _mediator.Send(command);
        return NoContent();
    }
}

public record UpdateDocumentFileTypeCategoryRequest(string Code, string? Description);
