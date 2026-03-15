using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.CreateDocumentFileType;
using RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileType;
using RESQ.Application.UseCases.Identity.Queries.GetAllDocumentFileTypes;

namespace RESQ.Presentation.Controllers.Identity;

[Route("identity/document-file-types")]
[ApiController]
public class DocumentFileTypesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>Lấy tất cả loại tài liệu (mặc định chỉ loại đang hoạt động).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = true)
    {
        var query = new GetAllDocumentFileTypesQuery(activeOnly);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Tạo loại tài liệu mới.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateDocumentFileTypeRequestDto dto)
    {
        var command = new CreateDocumentFileTypeCommand(
            dto.Code,
            dto.Name,
            dto.Description,
            dto.IsActive
        );
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { }, result);
    }

    /// <summary>Cập nhật loại tài liệu.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentFileTypeRequestDto dto)
    {
        var command = new UpdateDocumentFileTypeCommand(
            id,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.IsActive
        );
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
