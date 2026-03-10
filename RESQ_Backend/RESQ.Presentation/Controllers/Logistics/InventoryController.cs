using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;
using RESQ.Domain.Enum.Logistics;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/inventory")]
[ApiController]
public class InventoryController(IMediator mediator, ITokenService tokenService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly ITokenService _tokenService = tokenService;[HttpGet("depot/{depotId:int}")]
    public async Task<IActionResult> GetDepotInventory(
        int depotId,
        [FromQuery] List<int>? categoryIds,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetDepotInventoryQuery
        {
            DepotId = depotId,
            CategoryIds = categoryIds,
            ItemTypes = itemTypes,
            TargetGroups = targetGroups,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("my-depot")]
    public async Task<IActionResult> GetMyDepotInventory(
        [FromQuery] List<int>? categoryIds,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 10)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var query = new GetMyDepotInventoryQuery
        {
            UserId = Guid.Parse(userId),
            CategoryIds = categoryIds,
            ItemTypes = itemTypes,
            TargetGroups = targetGroups,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("metadata/categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetItemCategoriesMetadataQuery());
        return Ok(result);
    }[HttpGet("metadata/item-types")]
    public async Task<IActionResult> GetItemTypes()
    {
        var result = await _mediator.Send(new GetItemTypesQuery());
        return Ok(result);
    }

    [HttpGet("metadata/target-groups")]
    public async Task<IActionResult> GetTargetGroups()
    {
        var result = await _mediator.Send(new GetTargetGroupsQuery());
        return Ok(result);
    }
}
