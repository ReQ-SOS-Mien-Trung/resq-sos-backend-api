using MediatR;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.ImportInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;
using RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
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
    private readonly ITokenService _tokenService = tokenService;

    [HttpGet("depot/{depotId:int}")]
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
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var query = new GetMyDepotInventoryQuery
        {
            UserId = userId,
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
    }

    [HttpGet("metadata/organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        var result = await _mediator.Send(new GetOrganizationsMetadataQuery());
        return Ok(result);
    }

    [HttpGet("metadata/item-types")]
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

    [HttpGet("metadata/inventory-action-types")]
    public async Task<IActionResult> GetInventoryActionTypes()
    {
        var result = await _mediator.Send(new GetInventoryActionTypesQuery());
        return Ok(result);
    }

    [HttpGet("metadata/inventory-source-types")]
    public async Task<IActionResult> GetInventorySourceTypes()
    {
        var result = await _mediator.Send(new GetInventorySourceTypesQuery());
        return Ok(result);
    }

    [HttpGet("transactions/my-depot")]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] List<InventoryActionType>? actionTypes,
        [FromQuery] List<InventorySourceType>? sourceTypes,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var query = new GetInventoryTransactionHistoryQuery
        {
            UserId = userId,
            ActionTypes = actionTypes,
            SourceTypes = sourceTypes,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("stock-movements")]
    public async Task<IActionResult> GetInventoryLogs(
        [FromQuery] int? depotId,
        [FromQuery] int? reliefItemId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var isManager = User.HasClaim("RoleId", "4") || User.HasClaim(ClaimTypes.Role, "4");

        var query = new GetInventoryLogsQuery
        {
            UserId = userId,
            IsManager = isManager,
            DepotId = depotId,
            ReliefItemId = reliefItemId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportReliefItemsRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var command = new ImportReliefItemsCommand
        {
            UserId = userId,
            OrganizationId = request.OrganizationId,
            OrganizationName = request.OrganizationName,
            Items = request.Items
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
