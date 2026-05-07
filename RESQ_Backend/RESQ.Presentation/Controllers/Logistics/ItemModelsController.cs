using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Logistics.Commands.UpdateItemModel;
using RESQ.Application.UseCases.Logistics.Queries.GetItemModels;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/item-model")]
[ApiController]
public class ItemModelsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>Lấy danh sách tất cả item model với thông tin đầy đủ (admin).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? categoryId, [FromQuery] string? itemType, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetItemModelsQuery { CategoryId = categoryId, ItemType = itemType }, cancellationToken);
        return Ok(result);
    }

    /// <summary>Cập nhật item model.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemModelCommand command)
    {
        command.Id = id;
        command.RequestedBy = GetUserId();
        await _sender.Send(command);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId)) return userId;
        throw new UnauthorizedAccessException("Token không hợp lệ hoặc thiếu thông tin người dùng.");
    }
}
