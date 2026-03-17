using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.ImportInventory;
using RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;
using RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.PrepareSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;
using RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryByCode;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;
using RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Application.UseCases.Logistics.Queries.GetMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetReliefItemsByCategoryCode;
using RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Domain.Enum.Logistics;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/inventory")]
[ApiController]
public class InventoryController(IMediator mediator, ITokenService tokenService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly ITokenService _tokenService = tokenService;

    /// <summary>Xem tồn kho (phân trang) của một kho theo ID.</summary>
    [HttpGet("depot/{depotId:int}")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetDepotInventory(
        int depotId,
        [FromQuery(Name = "categoryCode")] List<ItemCategoryCode>? categoryCodes,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var categoryIds = await ResolveCategoryIdsAsync(categoryCodes, HttpContext.RequestAborted);

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

    /// <summary>Xem tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("my-depot")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetMyDepotInventory(
        [FromQuery(Name = "categoryCode")] List<ItemCategoryCode>? categoryCodes,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var categoryIds = await ResolveCategoryIdsAsync(categoryCodes, HttpContext.RequestAborted);

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

    /// <summary>Xem tổng số lượng tồn kho theo danh mục của một kho theo ID.</summary>
    [HttpGet("depot/{depotId:int}/quantity-by-category")]
    [Authorize]
    public async Task<IActionResult> GetDepotInventoryByCategory(int depotId)
    {
        var result = await _mediator.Send(new GetDepotInventoryByCategoryQuery(depotId));
        return Ok(result);
    }

    /// <summary>Xem tổng số lượng tồn kho theo danh mục của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("my-depot/quantity-by-category")]
    [Authorize]
    public async Task<IActionResult> GetMyDepotInventoryByCategory()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var result = await _mediator.Send(new GetMyDepotInventoryByCategoryQuery(userId));
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách danh mục vật tư.</summary>
    [HttpGet("metadata/categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetItemCategoriesMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách tổ chức tài trợ/quyên góp.</summary>
    [HttpGet("metadata/organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        var result = await _mediator.Send(new GetOrganizationsMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại vật tư (Equipment, Supply, Medicine, ...).</summary>
    [HttpGet("metadata/item-types")]
    public async Task<IActionResult> GetItemTypes()
    {
        var result = await _mediator.Send(new GetItemTypesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách nhóm đối tượng thụ hưởng (Adult, Child, Elderly, ...).</summary>
    [HttpGet("metadata/target-groups")]
    public async Task<IActionResult> GetTargetGroups()
    {
        var result = await _mediator.Send(new GetTargetGroupsQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại hành động kho (Import, Export, Adjust, ...).</summary>
    [HttpGet("metadata/inventory-action-types")]
    public async Task<IActionResult> GetInventoryActionTypes()
    {
        var result = await _mediator.Send(new GetInventoryActionTypesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách nguồn gốc vật tư (Purchase, Donation, Mission, ...).</summary>
    [HttpGet("metadata/inventory-source-types")]
    public async Task<IActionResult> GetInventorySourceTypes()
    {
        var result = await _mediator.Send(new GetInventorySourceTypesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách vật tư cứu trợ (ID - Tên).</summary>
    [HttpGet("metadata/relief-items")]
    public async Task<IActionResult> GetReliefItems()
    {
        var result = await _mediator.Send(new GetReliefItemMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách vật tư cứu trợ theo danh mục (ID - Tên).</summary>
    [HttpGet("metadata/relief-items/category/{categoryCode}")]
    public async Task<IActionResult> GetReliefItemsByCategoryCode(ItemCategoryCode categoryCode)
    {
        var result = await _mediator.Send(new GetReliefItemsByCategoryCodeQuery(categoryCode));
        return Ok(result);
    }

    /// <summary>Xem lịch sử biến động tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("stock-movements/my-depot")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
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

    /// <summary>Xem nhật ký xuất/nhập kho (phân trang) toàn hệ thống. Thủ kho chỉ xem được kho của mình.</summary>
    [HttpGet("stock-movements")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
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

    /// <summary>Xuất báo cáo biến động kho ra file Excel (ByDateRange / ByMonth).</summary>
    [HttpGet("export/movements")]
    public async Task<IActionResult> ExportMovements(
        [FromQuery] ExportPeriodType periodType,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var query = new ExportInventoryMovementQuery
        {
            UserId = userId,
            PeriodType = periodType,
            Month = month,
            Year = year,
            FromDate = fromDate,
            ToDate = toDate,
        };

        var result = await _mediator.Send(query);
        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>Tìm kiếm kho có chứa vật tư theo danh sách ID, ưu tiên kho gần và đủ số lượng.</summary>
    [HttpGet("search-depots")]
    [Authorize]
    public async Task<IActionResult> SearchDepotsByItems(
        [FromQuery] List<int> reliefItemIds,
        [FromQuery] List<int>? quantities,
        [FromQuery] bool activeDepotsOnly = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        if (quantities != null && quantities.Count > 0 && quantities.Count != reliefItemIds.Count)
            return BadRequest(new { message = "Số lượng quantities phải bằng số lượng reliefItemIds." });

        // Build per-item quantity dictionary (position-matched)
        var itemQuantities = new Dictionary<int, int>();
        if (quantities != null && quantities.Count == reliefItemIds.Count)
        {
            for (var i = 0; i < reliefItemIds.Count; i++)
                itemQuantities[reliefItemIds[i]] = quantities[i];
        }

        var query = new SearchWarehousesByItemsQuery
        {
            ReliefItemIds = reliefItemIds,
            ItemQuantities = itemQuantities,
            ManagerUserId = userId,
            ActiveDepotsOnly = activeDepotsOnly,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Nhập kho vật tư từ nguồn quyên góp của tổ chức.</summary>
    [HttpPost("import")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
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

    /// <summary>Nhập kho vật tư từ nguồn mua sắm theo hoá đơn.</summary>
    [HttpPost("import-purchase")]
    //[Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> ImportPurchase([FromBody] ImportPurchasedInventoryRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });
        }

        var command = new ImportPurchasedInventoryCommand
        {
            UserId = userId,
            Invoices = request.Invoices
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Tạo yêu cầu cung cấp vật tư từ một hoặc nhiều kho nguồn. Mỗi kho nguồn tạo một request riêng.</summary>
    [HttpPost("supply-requests")]
    [Authorize]
    public async Task<IActionResult> CreateSupplyRequest([FromBody] CreateSupplyRequestRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var command = new CreateSupplyRequestCommand
        {
            RequestingUserId = userId,
            Requests = request.Requests
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách yêu cầu tiếp tế của kho đang đăng nhập (cả hai chiều: gửi đi và nhận về).
    /// Trả về field <c>role</c>: "Requester" — kho này đã gửi yêu cầu | "Source" — kho này nhận yêu cầu.
    /// </summary>
    [HttpGet("supply-requests")]
    [Authorize]
    public async Task<IActionResult> GetSupplyRequests(
        [FromQuery] SourceDepotStatus? sourceStatus,
        [FromQuery] RequestingDepotStatus? requestingStatus,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var query = new GetSupplyRequestsQuery
        {
            UserId = userId,
            SourceStatus = sourceStatus,
            RequestingStatus = requestingStatus,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Manager kho nguồn bắt đầu đóng gói / picking (Accepted → Preparing).</summary>
    [HttpPut("supply-requests/{id:int}/prepare")]
    [Authorize]
    public async Task<IActionResult> PrepareSupplyRequest(int id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var result = await _mediator.Send(new PrepareSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn chấp nhận yêu cầu tiếp tế.</summary>
    [HttpPut("supply-requests/{id:int}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptSupplyRequest(int id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var result = await _mediator.Send(new AcceptSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn từ chối yêu cầu tiếp tế (bắt buộc ghi lý do).</summary>
    [HttpPut("supply-requests/{id:int}/reject")]
    [Authorize]
    public async Task<IActionResult> RejectSupplyRequest(int id, [FromBody] RejectSupplyRequestRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var command = new RejectSupplyRequestCommand
        {
            SupplyRequestId = id,
            UserId = userId,
            Reason = request.Reason
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Manager kho nguồn xuất hàng và vận chuyển đến kho yêu cầu.</summary>
    [HttpPut("supply-requests/{id:int}/ship")]
    [Authorize]
    public async Task<IActionResult> ShipSupplyRequest(int id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var result = await _mediator.Send(new ShipSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn xác nhận đã hoàn tất giao hàng (Shipped → Completed).</summary>
    [HttpPut("supply-requests/{id:int}/complete")]
    [Authorize]
    public async Task<IActionResult> CompleteSupplyRequest(int id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var result = await _mediator.Send(new CompleteSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho yêu cầu xác nhận đã nhận hàng tiếp tế.</summary>
    [HttpPut("supply-requests/{id:int}/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmSupplyRequest(int id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ hoặc không tìm thấy thông tin người dùng." });

        var result = await _mediator.Send(new ConfirmSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    private async Task<List<int>?> ResolveCategoryIdsAsync(
        List<ItemCategoryCode>? categoryCodes,
        CancellationToken cancellationToken)
    {
        if (categoryCodes == null || categoryCodes.Count == 0)
        {
            return null;
        }

        var categoryTasks = categoryCodes
            .Distinct()
            .Select(code => _mediator.Send(new GetItemCategoryByCodeQuery(code), cancellationToken));

        var categories = await Task.WhenAll(categoryTasks);

        return categories.Select(category => category.Id).ToList();
    }
}
