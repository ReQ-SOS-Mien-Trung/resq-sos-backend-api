//using MediatR;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using RESQ.Application.Common.Constants;
//using RESQ.Application.Services;
//using RESQ.Application.UseCases.Logistics.Commands.ImportInventory;
//using RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;
//using RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;
//using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
//using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
//using RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;
//using RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;
//using RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;
//using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
//using RESQ.Application.UseCases.Logistics.Queries.GetMetadata;
//using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;
//using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventoryByCategory;
//using RESQ.Domain.Enum.Logistics;
//using System.Security.Claims;

//namespace RESQ.Presentation.Controllers.Logistics;

//[Route("logistics/inventory")]
//[ApiController]
//public class InventoryController(IMediator mediator, ITokenService tokenService) : ControllerBase
//{
//    private readonly IMediator _mediator = mediator;
//    private readonly ITokenService _tokenService = tokenService;

//    /// <summary>Xem tồn kho (phân trang) của một kho theo ID.</summary>
//    [HttpGet("depot/{depotId:int}")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
//    public async Task<IActionResult> GetDepotInventory(
//        int depotId,
//        [FromQuery] List<int>? categoryIds,
//        [FromQuery] List<ItemType>? itemTypes,
//        [FromQuery] List<TargetGroup>? targetGroups,
//        [FromQuery] int pageNumber = 1,
//        [FromQuery] int pageSize = 10)
//    {
//        var query = new GetDepotInventoryQuery
//        {
//            DepotId = depotId,
//            CategoryIds = categoryIds,
//            ItemTypes = itemTypes,
//            TargetGroups = targetGroups,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };

//        var result = await _mediator.Send(query);
//        return Ok(result);
//    }

//    /// <summary>Xem tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
//    [HttpGet("my-depot")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
//    public async Task<IActionResult> GetMyDepotInventory(
//        [FromQuery] List<int>? categoryIds,
//        [FromQuery] List<ItemType>? itemTypes,
//        [FromQuery] List<TargetGroup>? targetGroups,
//        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var query = new GetMyDepotInventoryQuery
//        {
//            UserId = userId,
//            CategoryIds = categoryIds,
//            ItemTypes = itemTypes,
//            TargetGroups = targetGroups,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };

//        var result = await _mediator.Send(query);
//        return Ok(result);
//    }

//    /// <summary>Xem tổng số lượng tồn kho theo danh mục của một kho theo ID.</summary>
//    [HttpGet("depot/{depotId:int}/quantity-by-category")]
//    [Authorize]
//    public async Task<IActionResult> GetDepotInventoryByCategory(int depotId)
//    {
//        var result = await _mediator.Send(new GetDepotInventoryByCategoryQuery(depotId));
//        return Ok(result);
//    }

//    /// <summary>Xem tổng số lượng tồn kho theo danh mục của kho do người dùng hiện tại quản lý.</summary>
//    [HttpGet("my-depot/quantity-by-category")]
//    [Authorize]
//    public async Task<IActionResult> GetMyDepotInventoryByCategory()
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var result = await _mediator.Send(new GetMyDepotInventoryByCategoryQuery(userId));
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách danh mục vật tư.</summary>
//    [HttpGet("metadata/categories")]
//    public async Task<IActionResult> GetCategories()
//    {
//        var result = await _mediator.Send(new GetItemCategoriesMetadataQuery());
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách tổ chức tài trợ/quyên góp.</summary>
//    [HttpGet("metadata/organizations")]
//    public async Task<IActionResult> GetOrganizations()
//    {
//        var result = await _mediator.Send(new GetOrganizationsMetadataQuery());
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách loại vật tư (Equipment, Supply, Medicine, ...).</summary>
//    [HttpGet("metadata/item-types")]
//    public async Task<IActionResult> GetItemTypes()
//    {
//        var result = await _mediator.Send(new GetItemTypesQuery());
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách nhóm đối tượng thụ hưởng (Adult, Child, Elderly, ...).</summary>
//    [HttpGet("metadata/target-groups")]
//    public async Task<IActionResult> GetTargetGroups()
//    {
//        var result = await _mediator.Send(new GetTargetGroupsQuery());
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách loại hành động kho (Import, Export, Adjust, ...).</summary>
//    [HttpGet("metadata/inventory-action-types")]
//    public async Task<IActionResult> GetInventoryActionTypes()
//    {
//        var result = await _mediator.Send(new GetInventoryActionTypesQuery());
//        return Ok(result);
//    }

//    /// <summary>[Metadata] Danh sách nguồn gốc vật tư (Purchase, Donation, Mission, ...).</summary>
//    [HttpGet("metadata/inventory-source-types")]
//    public async Task<IActionResult> GetInventorySourceTypes()
//    {
//        var result = await _mediator.Send(new GetInventorySourceTypesQuery());
//        return Ok(result);
//    }

//    /// <summary>Xem lịch sử biến động tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
//    [HttpGet("stock-movements/my-depot")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
//    public async Task<IActionResult> GetTransactionHistory(
//        [FromQuery] List<InventoryActionType>? actionTypes,
//        [FromQuery] List<InventorySourceType>? sourceTypes,
//        [FromQuery] DateTime? fromDate,
//        [FromQuery] DateTime? toDate,
//        [FromQuery] int pageNumber = 1,
//        [FromQuery] int pageSize = 10)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var query = new GetInventoryTransactionHistoryQuery
//        {
//            UserId = userId,
//            ActionTypes = actionTypes,
//            SourceTypes = sourceTypes,
//            FromDate = fromDate,
//            ToDate = toDate,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };

//        var result = await _mediator.Send(query);
//        return Ok(result);
//    }

//    /// <summary>Xem nhật ký xuất/nhập kho (phân trang) toàn hệ thống. Thủ kho chỉ xem được kho của mình.</summary>
//    [HttpGet("stock-movements")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
//    public async Task<IActionResult> GetInventoryLogs(
//        [FromQuery] int? depotId,
//        [FromQuery] int? reliefItemId,
//        [FromQuery] int pageNumber = 1,
//        [FromQuery] int pageSize = 10)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var isManager = User.HasClaim("RoleId", "4") || User.HasClaim(ClaimTypes.Role, "4");

//        var query = new GetInventoryLogsQuery
//        {
//            UserId = userId,
//            IsManager = isManager,
//            DepotId = depotId,
//            ReliefItemId = reliefItemId,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };

//        var result = await _mediator.Send(query);
//        return Ok(result);
//    }

//    /// <summary>Xuất báo cáo biến động kho ra file Excel (ByMonth / ByYear / ByMonthRange).</summary>
//    [HttpGet("export/movements")]
//    public async Task<IActionResult> ExportMovements(
//        [FromQuery] ExportPeriodType periodType,
//        [FromQuery] int? month     = null,
//        [FromQuery] int? year      = null,
//        [FromQuery] int? fromMonth = null,
//        [FromQuery] int? fromYear  = null,
//        [FromQuery] int? toMonth   = null,
//        [FromQuery] int? toYear    = null)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

//        var query = new ExportInventoryMovementQuery
//        {
//            UserId     = userId,
//            PeriodType = periodType,
//            Month      = month,
//            Year       = year,
//            FromMonth  = fromMonth,
//            FromYear   = fromYear,
//            ToMonth    = toMonth,
//            ToYear     = toYear,
//        };

//        var result = await _mediator.Send(query);
//        return File(result.FileContent, result.ContentType, result.FileName);
//    }

//    /// <summary>Nhập kho vật tư từ nguồn quyên góp của tổ chức.</summary>
//    [HttpPost("import")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
//    public async Task<IActionResult> Import([FromBody] ImportReliefItemsRequest request)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var command = new ImportReliefItemsCommand
//        {
//            UserId = userId,
//            OrganizationId = request.OrganizationId,
//            OrganizationName = request.OrganizationName,
//            Items = request.Items
//        };

//        var result = await _mediator.Send(command);
//        return Ok(result);
//    }

//    /// <summary>Nhập kho vật tư từ nguồn mua sắm theo hoá đơn.</summary>
//    [HttpPost("import-purchase")]
//    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
//    public async Task<IActionResult> ImportPurchase([FromBody] ImportPurchasedInventoryRequest request)
//    {
//        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
//        {
//            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
//        }

//        var command = new ImportPurchasedInventoryCommand
//        {
//            UserId = userId,
//            Invoices = request.Invoices
//        };

//        var result = await _mediator.Send(command);
//        return Ok(result);
//    }
//}
