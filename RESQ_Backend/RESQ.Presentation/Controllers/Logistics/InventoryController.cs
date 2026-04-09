using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.UseCases.Logistics.Commands.ImportInventory;
using RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;
using RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;
using RESQ.Application.UseCases.Logistics.Commands.ExportInventory;
using RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;
using RESQ.Application.UseCases.Logistics.Commands.PrepareSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;
using RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;
using RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;
using RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;
using RESQ.Application.UseCases.Logistics.Queries.GenerateDonationImportTemplate;
using RESQ.Application.UseCases.Logistics.Queries.GeneratePurchaseImportTemplate;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryByCode;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryLots;
using RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.GetMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetMyPickupHistoryActivities;
using RESQ.Application.UseCases.Logistics.Queries.GetMyReturnHistoryActivities;
using RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholds;
using RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingPickupActivities;
using RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;
using RESQ.Application.UseCases.Logistics.Queries.GetAdminThresholds;
using RESQ.Application.UseCases.Logistics.Queries.GetReliefItemsByCategoryCode;
using RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequestPriorityConfig;
using RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;
using RESQ.Application.UseCases.Logistics.Queries.GetWarningBandConfig;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Logistics;
using System.Security.Claims;

using RESQ.Application.Repositories.Logistics;

namespace RESQ.Presentation.Controllers.Logistics;

[Route("logistics/inventory")]
[ApiController]
public class InventoryController(IMediator mediator, IItemCategoryRepository itemCategoryRepository, IAuthorizationService authorizationService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IItemCategoryRepository _itemCategoryRepository = itemCategoryRepository;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    /// <summary>Xem tồn kho (phân trang) của một kho theo ID.</summary>
    [HttpGet("depot/{depotId:int}")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetDepotInventory(
        int depotId,
        [FromQuery(Name = "categoryCode")] List<ItemCategoryCode>? categoryCodes,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] string? itemName,
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
            ItemName = itemName,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>Xem tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("my-depot")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetMyDepotInventory(
        [FromQuery(Name = "categoryCode")] List<ItemCategoryCode>? categoryCodes,
        [FromQuery] List<ItemType>? itemTypes,
        [FromQuery] List<TargetGroup>? targetGroups,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

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

    /// <summary>[Manager] Xem danh sách activity lấy hàng sắp tới của kho mình quản lý.</summary>
    [HttpGet("my-depot/upcoming-pickups")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(PagedResult<UpcomingPickupActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyUpcomingPickups(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyUpcomingPickupActivitiesQuery(userId)
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(result);
    }

    /// <summary>[Manager] Xem lịch sử các activity đã đến kho mình quản lý để lấy vật tư thành công.</summary>
    [HttpGet("my-depot/pickup-history")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(PagedResult<PickupHistoryActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPickupHistory(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyPickupHistoryActivitiesQuery(userId)
        {
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(result);
    }

    /// <summary>[Manager] Xem danh sách activity trả đồ đang trên đường về kho hoặc đang chờ kho xác nhận.</summary>
    [HttpGet("my-depot/upcoming-returns")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(PagedResult<UpcomingReturnActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyUpcomingReturns(
        [FromQuery] MissionActivityStatus status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyUpcomingReturnActivitiesQuery(userId)
        {
            Status = status,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(result);
    }

    /// <summary>[Manager] Xem lịch sử các activity trả đồ đã được kho mình quản lý xác nhận thành công.</summary>
    [HttpGet("my-depot/return-history")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(PagedResult<ReturnHistoryActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyReturnHistory(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyReturnHistoryActivitiesQuery(userId)
        {
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(result);
    }

    /// <summary>[Admin] Dữ liệu biểu đồ vật tư sắp hết trên tất cả kho hoặc một kho cụ thể.
    /// severityRatio = max(0, available / minimumThreshold). Ngưỡng được resolve theo cấu hình scope.
    /// Response gồm: summary (tổng), byDepot (bar chart), byCategory (pie chart), items (table).</summary>
    [HttpGet("low-stock")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    [ProducesResponseType(typeof(LowStockChartResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockItems(
        [FromQuery] int? depotId = null,
        [FromQuery] string? warningLevel = null,
        [FromQuery] bool includeUnconfigured = false)
    {
        var result = await _mediator.Send(new GetLowStockItemsQuery(depotId, warningLevel, includeUnconfigured));
        return Ok(result);
    }

    /// <summary>[Manager] Dữ liệu biểu đồ vật tư sắp hết tại kho mình quản lý.
    /// severityRatio = max(0, available / minimumThreshold). Ngưỡng được resolve theo cấu hình scope.
    /// Response gồm: summary (tổng), byDepot (bar chart), byCategory (pie chart), items (table).</summary>
    [HttpGet("my-depot/low-stock")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(LowStockChartResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDepotLowStockItems(
        [FromQuery] string? warningLevel = null,
        [FromQuery] bool includeUnconfigured = false)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyDepotLowStockQuery(userId, warningLevel, includeUnconfigured));
        return Ok(result);
    }

    /// <summary>[Manager] Xem cấu hình threshold hiện tại của kho mình quản lý (global + override theo scope).</summary>
    [HttpGet("my-depot/thresholds")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(GetMyDepotThresholdsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDepotThresholds()
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetMyDepotThresholdsQuery(userId));
        return Ok(result);
    }

    // NOTE: Đã tắt - không còn lưu lịch sử thay đổi threshold nữa (UpsertAsync/ResetAsync ghi đè trực tiếp)
    // [HttpGet("my-depot/thresholds/history")]
    // public async Task<IActionResult> GetMyDepotThresholdHistory(...) { ... }

    /// <summary>[Admin/Manager] Cập nhật ngưỡng tồn kho. Caller có quyền toàn cục chỉ được cấu hình scope Global; caller quản lý kho chỉ được cấu hình Depot/DepotCategory/DepotItem của kho mình quản lý.</summary>
    [HttpPut("my-depot/thresholds")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(StockThresholdCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMyDepotThreshold([FromBody] UpdateMyDepotThresholdRequest request)
    {
        var userId = GetCurrentUserId();
        var canManageGlobalThresholds = (await _authorizationService
            .AuthorizeAsync(User, null, PermissionConstants.SystemConfigManage))
            .Succeeded;

        var result = await _mediator.Send(new UpdateMyDepotThresholdCommand
        {
            UserId = userId,
            CanManageGlobalThresholds = canManageGlobalThresholds,
            ScopeType = request.ScopeType,
            CategoryId = request.CategoryId,
            ItemModelId = request.ItemModelId,
            MinimumThreshold = request.MinimumThreshold,
            RowVersion = request.RowVersion,
            Reason = request.Reason
        });

        return Ok(result);
    }

    /// <summary>[Manager] Reset cấu hình ngưỡng tồn kho về scope thấp hơn (soft reset: is_active=false).</summary>
    [HttpDelete("my-depot/thresholds")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(StockThresholdCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetMyDepotThreshold([FromBody] ResetMyDepotThresholdRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new ResetMyDepotThresholdCommand
        {
            UserId = userId,
            ScopeType = request.ScopeType,
            CategoryId = request.CategoryId,
            ItemModelId = request.ItemModelId,
            RowVersion = request.RowVersion,
            Reason = request.Reason
        });

        return Ok(result);
    }

    // NOTE: Đã tắt - không còn có cấu hình inactive để restore (UpsertAsync ghi đè trực tiếp, ResetAsync xóa cứng)
    // [HttpPut("my-depot/thresholds/restore")]
    // public async Task<IActionResult> RestoreThreshold(...) { ... }

    /// <summary>[Admin] Xem cấu hình ngưỡng tồn kho hiện tại (global + override theo scope). Truyền thêm depotId để xem cấu hình override của một kho cụ thể.</summary>
    [HttpGet("thresholds")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(GetAdminThresholdsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminThresholds([FromQuery] int? depotId = null)
    {
        var result = await _mediator.Send(new GetAdminThresholdsQuery(depotId));
        return Ok(result);
    }

    // NOTE: Đã tắt - không còn lưu lịch sử thay đổi threshold nữa
    // [HttpGet("thresholds/history")]
    // public async Task<IActionResult> GetAdminThresholdHistory(...) { ... }

    /// <summary>[Admin] Xem cấu hình warning bands hiện tại (N-band, lưu trong DB). Dùng để frontend hiển thị/chỉnh sửa.</summary>
    [HttpGet("warning-band-config")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(WarningBandConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWarningBandConfig()
    {
        var result = await _mediator.Send(new GetWarningBandConfigQuery());
        if (result == null)
            return NotFound("Chưa có cấu hình warning band nào trong hệ thống.");
        return Ok(result);
    }

    /// <summary>[Admin] Cập nhật cấu hình 4 bậc ngưỡng tồn kho. Chỉ nhập giới hạn trên (%) cho 3 bậc đầu; backend tự tính From từ bậc trước.</summary>
    [HttpPut("warning-band-config")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(WarningBandConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertWarningBandConfig([FromBody] UpsertWarningBandRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new UpsertWarningBandConfigCommand
        {
            UserId = userId,
            Request = request
        });
        return Ok(result);
    }

    /// <summary>[Admin] Xem cấu hình thời gian phản hồi cho 3 mức độ yêu cầu tiếp tế.</summary>
    [HttpGet("supply-request-priority-config")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(GetSupplyRequestPriorityConfigResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplyRequestPriorityConfig()
    {
        var result = await _mediator.Send(new GetSupplyRequestPriorityConfigQuery());
        return Ok(result);
    }

    /// <summary>[Admin] Cập nhật cấu hình thời gian phản hồi cho 3 mức độ yêu cầu tiếp tế.</summary>
    [HttpPut("supply-request-priority-config")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
    [ProducesResponseType(typeof(UpsertSupplyRequestPriorityConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertSupplyRequestPriorityConfig([FromBody] UpsertSupplyRequestPriorityConfigRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new UpsertSupplyRequestPriorityConfigCommand
        {
            UserId = userId,
            UrgentMinutes = request.UrgentMinutes,
            HighMinutes = request.HighMinutes,
            MediumMinutes = request.MediumMinutes
        });

        return Ok(result);
    }

    /// <summary>Xem danh sách lô hàng (lots) của một item model trong kho (FEFO).</summary>
    [HttpGet("{itemModelId:int}/lots")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetInventoryLots(
        int itemModelId,
        [FromQuery] int? depotId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();

        var query = new GetInventoryLotsQuery
        {
            UserId = userId,
            ItemModelId = itemModelId,
            DepotId = depotId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>Xem tổng số lượng tồn kho theo danh mục của một kho theo ID.</summary>
    [HttpGet("depot/{depotId:int}/quantity-by-category")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryRead)]
    public async Task<IActionResult> GetDepotInventoryByCategory(int depotId)
    {
        var result = await _mediator.Send(new GetDepotInventoryByCategoryQuery(depotId));
        return Ok(result);
    }

    /// <summary>Xem tổng số lượng tồn kho theo danh mục của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("my-depot/quantity-by-category")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> GetMyDepotInventoryByCategory()
    {
        var userId = GetCurrentUserId();

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

    /// <summary>[Metadata] Danh sách trạng thái kho cứu trợ.</summary>
    [HttpGet("metadata/depot-statuses")]
    public async Task<IActionResult> GetDepotStatuses()
    {
        var result = await _mediator.Send(new GetDepotStatusesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách kiểu khoảng thời gian xuất báo cáo.</summary>
    [HttpGet("metadata/export-period-types")]
    public async Task<IActionResult> GetExportPeriodTypes()
    {
        var result = await _mediator.Send(new GetExportPeriodTypesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách mã danh mục vật tư (enum).</summary>
    [HttpGet("metadata/item-category-codes")]
    public async Task<IActionResult> GetItemCategoryCodes()
    {
        var result = await _mediator.Send(new GetItemCategoryCodesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Trạng thái yêu cầu tiếp tế theo góc nhìn kho yêu cầu.</summary>
    [HttpGet("metadata/requesting-depot-statuses")]
    public async Task<IActionResult> GetRequestingDepotStatuses()
    {
        var result = await _mediator.Send(new GetRequestingDepotStatusesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Tình trạng vật phẩm tái sử dụng (Good, Fair, Poor).</summary>
    [HttpGet("metadata/reusable-item-conditions")]
    public async Task<IActionResult> GetReusableItemConditions()
    {
        var result = await _mediator.Send(new GetReusableItemConditionsQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Trạng thái vật phẩm tái sử dụng (Available, InUse, Maintenance, Decommissioned).</summary>
    [HttpGet("metadata/reusable-item-statuses")]
    public async Task<IActionResult> GetReusableItemStatuses()
    {
        var result = await _mediator.Send(new GetReusableItemStatusesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Trạng thái yêu cầu tiếp tế theo góc nhìn kho nguồn.</summary>
    [HttpGet("metadata/source-depot-statuses")]
    public async Task<IActionResult> GetSourceDepotStatuses()
    {
        var result = await _mediator.Send(new GetSourceDepotStatusesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách mức độ ưu tiên yêu cầu tiếp tế (key tiếng Anh, value tiếng Việt).</summary>
    [HttpGet("metadata/supply-request-priority-levels")]
    public async Task<IActionResult> GetSupplyRequestPriorityLevels()
    {
        var result = await _mediator.Send(new GetSupplyRequestPriorityLevelsQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách loại phạm vi ngưỡng tồn kho (scope type).</summary>
    [HttpGet("metadata/scope-types")]
    public async Task<IActionResult> GetStockThresholdScopeTypes()
    {
        var result = await _mediator.Send(new GetStockThresholdScopeTypesQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách vật tư cứu trợ (ID - Tên).</summary>
    [HttpGet("metadata/item-models")]
    public async Task<IActionResult> GetReliefItems()
    {
        var result = await _mediator.Send(new GetReliefItemMetadataQuery());
        return Ok(result);
    }

    /// <summary>[Metadata] Danh sách vật tư cứu trợ theo danh mục (ID - Tên).</summary>
    [HttpGet("metadata/item-models/category/{categoryCode}")]
    public async Task<IActionResult> GetReliefItemsByCategoryCode(ItemCategoryCode categoryCode)
    {
        var result = await _mediator.Send(new GetReliefItemsByCategoryCodeQuery(categoryCode));
        return Ok(result);
    }

    /// <summary>Xem lịch sử biến động tồn kho (phân trang) của kho do người dùng hiện tại quản lý.</summary>
    [HttpGet("stock-movements/my-depot")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] List<InventoryActionType>? actionTypes,
        [FromQuery] List<InventorySourceType>? sourceTypes,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

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
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> GetInventoryLogs(
        [FromQuery] int? depotId,
        [FromQuery] int? itemModelId,
        [FromQuery] List<InventoryActionType>? actionTypes,
        [FromQuery] List<InventorySourceType>? sourceTypes,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

        var canManageAnyInventory = (await _authorizationService
            .AuthorizeAsync(User, null, PermissionConstants.SystemConfigManage))
            .Succeeded;

        var isManager = !canManageAnyInventory;

        var query = new GetInventoryLogsQuery
        {
            UserId = userId,
            IsManager = isManager,
            DepotId = depotId,
            ItemModelId = itemModelId,
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

    /// <summary>Xuất báo cáo biến động kho ra file Excel (ByDateRange / ByMonth).</summary>
    [HttpGet("export/movements")]
    public async Task<IActionResult> ExportMovements(
        [FromQuery] ExportPeriodType periodType,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var userId = GetCurrentUserId();

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

    /// <summary>Tải file Excel mẫu nhập kho từ thiện (donation import template).
    /// File có dropdown chọn danh mục -> dependent dropdown chọn vật phẩm,
    /// và auto-fill VLOOKUP cho Đối tượng / Loại vật phẩm / Đơn vị.</summary>
    [HttpGet("template/donation-import")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDonationImportTemplate()
    {
        var result = await _mediator.Send(new GenerateDonationImportTemplateQuery());
        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>Tải file Excel mẫu nhập kho mua sắm (purchase import template).
    /// File có cột thông tin hóa đơn VAT, dropdown danh mục -> vật phẩm,
    /// auto-fill VLOOKUP và cột đơn giá.</summary>
    [HttpGet("template/purchase-import")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPurchaseImportTemplate()
    {
        var result = await _mediator.Send(new GeneratePurchaseImportTemplateQuery());
        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>Tìm kiếm kho có chứa vật tư theo danh sách ID, ưu tiên kho gần và đủ số lượng.</summary>
    [HttpGet("search-depots")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> SearchDepotsByItems(
        [FromQuery] List<int> itemModelIds,
        [FromQuery] List<int>? quantities,
        [FromQuery] bool activeDepotsOnly = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

        if (quantities != null && quantities.Count > 0 && quantities.Count != itemModelIds.Count)
            throw new BadRequestException("Số lượng quantities phải bằng số lượng itemModelIds.");

        // Build per-item quantity dictionary (position-matched)
        var itemQuantities = new Dictionary<int, int>();
        if (quantities != null && quantities.Count == itemModelIds.Count)
        {
            for (var i = 0; i < itemModelIds.Count; i++)
                itemQuantities[itemModelIds[i]] = quantities[i];
        }

        var query = new SearchWarehousesByItemsQuery
        {
            ItemModelIds = itemModelIds,
            ItemQuantities = itemQuantities,
            ManagerUserId = userId,
            ActiveDepotsOnly = activeDepotsOnly,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>[Manager] Xuất kho thủ công (Export): giảm tồn kho theo FEFO và ghi nhật ký.
    /// Chỉ xuất được số lượng khả dụng (Quantity - ReservedQuantity).</summary>
    [HttpPost("my-depot/export")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(ExportInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportInventory([FromBody] ExportInventoryRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new ExportInventoryCommand(
            userId,
            request.ItemModelId,
            request.Quantity,
            request.Note));
        return Ok(result);
    }

    /// <summary>[Manager] Điều chỉnh tồn kho (Adjust): quantityChange dương → tạo lô mới + tăng số lượng;
    /// quantityChange âm → FEFO deduction trên các lô + giảm số lượng. Bắt buộc ghi lý do.</summary>
    [HttpPost("my-depot/adjust")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(AdjustInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustInventory([FromBody] AdjustInventoryRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new AdjustInventoryCommand(
            userId,
            request.ItemModelId,
            request.QuantityChange,
            request.Reason,
            request.Note,
            request.ExpiredDate));
        return Ok(result);
    }

    /// <summary>Nhập kho vật tư từ nguồn quyên góp của tổ chức.</summary>
    [HttpPost("import")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> Import([FromBody] ImportReliefItemsRequest request)
    {
        var userId = GetCurrentUserId();

        var command = new ImportReliefItemsCommand
        {
            UserId = userId,
            OrganizationId = request.OrganizationId,
            OrganizationName = request.OrganizationName,
            BatchNote = request.BatchNote,
            Items = request.Items
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Nhập kho vật tư từ nguồn mua sắm theo hóa đơn.</summary>
    [HttpPost("import-purchase")]
    [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
    public async Task<IActionResult> ImportPurchase([FromBody] ImportPurchasedInventoryRequest request)
    {
        var userId = GetCurrentUserId();

        var command = new ImportPurchasedInventoryCommand
        {
            UserId = userId,
            DepotFundId = request.DepotFundId,
            AdvancedByName = request.AdvancedByName,
            Invoices = request.Invoices
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Tạo yêu cầu cung cấp vật tư từ một hoặc nhiều kho nguồn. Mỗi kho nguồn tạo một request riêng.</summary>
    [HttpPost("supply-requests")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> CreateSupplyRequest([FromBody] CreateSupplyRequestRequest request)
    {
        var userId = GetCurrentUserId();

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
    /// Trả về field <c>role</c>: "Requester" - kho này đã gửi yêu cầu | "Source" - kho này nhận yêu cầu.
    /// </summary>
    [HttpGet("supply-requests")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    [ProducesResponseType(typeof(GetSupplyRequestsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplyRequests(
        [FromQuery] SourceDepotStatus? sourceStatus,
        [FromQuery] RequestingDepotStatus? requestingStatus,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

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

    /// <summary>Manager kho nguồn bắt đầu đóng gói / picking (Accepted -> Preparing).</summary>
    [HttpPut("supply-requests/{id:int}/prepare")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> PrepareSupplyRequest(int id)
    {
        var userId = GetCurrentUserId();

        var result = await _mediator.Send(new PrepareSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn chấp nhận yêu cầu tiếp tế.</summary>
    [HttpPut("supply-requests/{id:int}/accept")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> AcceptSupplyRequest(int id)
    {
        var userId = GetCurrentUserId();

        var result = await _mediator.Send(new AcceptSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn từ chối yêu cầu tiếp tế (bắt buộc ghi lý do).</summary>
    [HttpPut("supply-requests/{id:int}/reject")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> RejectSupplyRequest(int id, [FromBody] RejectSupplyRequestRequest request)
    {
        var userId = GetCurrentUserId();

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
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> ShipSupplyRequest(int id)
    {
        var userId = GetCurrentUserId();

        var result = await _mediator.Send(new ShipSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho nguồn xác nhận đã hoàn tất giao hàng (Shipping -> Completed).</summary>
    [HttpPut("supply-requests/{id:int}/complete")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> CompleteSupplyRequest(int id)
    {
        var userId = GetCurrentUserId();

        var result = await _mediator.Send(new CompleteSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    /// <summary>Manager kho yêu cầu xác nhận đã nhận hàng tiếp tế.</summary>
    [HttpPut("supply-requests/{id:int}/confirm")]
    [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
    public async Task<IActionResult> ConfirmSupplyRequest(int id)
    {
        var userId = GetCurrentUserId();

        var result = await _mediator.Send(new ConfirmSupplyRequestCommand(id, userId));
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            throw new UnauthorizedException("Token không hợp lệ hoặc không tìm thấy thông tin người dùng.");
        return userId;
    }

    private async Task<List<int>?> ResolveCategoryIdsAsync(
        List<ItemCategoryCode>? categoryCodes,
        CancellationToken cancellationToken)
    {
        if (categoryCodes == null || categoryCodes.Count == 0)
            return null;

        var ids = await _itemCategoryRepository.GetIdsByCodesAsync(categoryCodes, cancellationToken);
        return ids.Count > 0 ? ids : null;
    }
}
