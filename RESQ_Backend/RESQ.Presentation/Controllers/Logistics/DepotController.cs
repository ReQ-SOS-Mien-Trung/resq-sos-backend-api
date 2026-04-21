using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;
using RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;
using RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;
using RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;
using RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;
using RESQ.Application.UseCases.Logistics.Commands.CompleteClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.StartDepotClosing;
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
using RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;
using RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Logistics.Queries.GetAvailableManagersMetadata;
using RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotById;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotsByCluster;
using RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions;
using RESQ.Application.UseCases.Logistics.Queries.GetMyClosureTransfers;
using RESQ.Application.UseCases.Logistics.Queries.GetMyIncomingClosureTransfer;
using RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;
using RESQ.Application.Services;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot")]
    [ApiController]
    public class DepotController(
        IMediator mediator,
        IManagerDepotAccessService managerDepotAccessService,
        IOperationalHubService operationalHubService) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
        private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
        private readonly IOperationalHubService _operationalHubService = operationalHubService;

        /// <summary>Lấy danh sách tất cả kho có phân trang.</summary>
        [HttpGet]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        public async Task<IActionResult> Get(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] List<DepotStatus>? statuses = null,
            [FromQuery] string? search = null)
        {
            var query = new GetAllDepotsQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                Statuses = statuses,
                Search = search
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Xem chi tiết một kho theo ID.</summary>
        [HttpGet("{id}")]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _mediator.Send(new GetDepotByIdQuery(id));
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách kho sắp xếp theo khoảng cách gần nhất so với cluster SOS.
        /// Chỉ trả về kho đang Available và còn hàng, trong bán kính cấu hình
        /// dùng chung với endpoint lấy đội cứu hộ gần cluster.
        /// </summary>
        [HttpGet("by-cluster/{clusterId}")]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        public async Task<IActionResult> GetByCluster(int clusterId)
        {
            var result = await _mediator.Send(new GetDepotsByClusterQuery(clusterId));
            return Ok(result);
        }

        /// <summary>Tạo kho mới. Manager là optional — nếu gán ngay thì kho chuyển sang Available.</summary>
        [HttpPost]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        public async Task<IActionResult> Create([FromBody] CreateDepotRequestDto dto)
        {
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                dto.Latitude,
                dto.Longitude,
                dto.Capacity,
                dto.WeightCapacity,
                dto.ManagerId,
                dto.ImageUrl,
                GetUserId()
            );

            var result = await _mediator.Send(command);
            return StatusCode(201, result);
        }

        /// <summary>Cập nhật thông tin kho.</summary>
        [HttpPut("{id}")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDepotRequestDto dto)
        {
            // Pass primitives to command.
            var command = new UpdateDepotCommand(
                id,
                dto.Name,
                dto.Address,
                dto.Latitude,
                dto.Longitude,
                dto.Capacity,
                dto.WeightCapacity,
                dto.ImageUrl,
                GetUserId()
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Gán thêm manager cho kho. Manager cũ không bị gỡ tự động — việc gỡ manager là thao tác riêng biệt (DELETE).
        /// Một manager có thể quản lý nhiều kho cùng lúc. Kho chuyển sang trạng thái Available sau khi gán.
        /// </summary>
        [HttpPatch("{id}/manager")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(AssignDepotManagerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignManager(int id, [FromBody] AssignDepotManagerRequestDto dto)
        {
            var command = new AssignDepotManagerCommand(id, dto.ManagerIds, GetUserId());
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Gỡ manager khỏi kho. Manager cũ bị unassign (UnassignedAt được set) nhưng lịch sử vẫn được giữ lại.
        /// Kho chuyển sang trạng thái PendingAssignment sau khi gỡ.
        /// </summary>
        [HttpDelete("{id}/manager")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(UnassignDepotManagerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnassignManager(int id)
        {
            var command = new UnassignDepotManagerCommand(id, GetUserId());
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Thay đổi trạng thái kho (Available / Unavailable). Admin có thể đổi bất kỳ kho, Manager chỉ đổi kho mình quản lý.</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        public async Task<IActionResult> ChangeStatus(int id, [FromQuery] ChangeDepotStatusRequestDto dto)
        {
            var depotStatus = dto.Status switch
            {
                ChangeableDepotStatus.Available   => DepotStatus.Available,
                ChangeableDepotStatus.Unavailable => DepotStatus.Unavailable,
                _ => throw new BadHttpRequestException("Trạng thái kho không hợp lệ.")
            };
            var command = new ChangeDepotStatusCommand(id, depotStatus, GetUserId(), id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Chuyển kho sang trạng thái Closing để bắt đầu quy trình đóng kho.
        /// Chặn khi còn đơn tiếp tế, vật phẩm đang bị ràng buộc với nhiệm vụ, hoặc đang có phiên chuyển kho/đóng kho dở dang.
        /// </summary>
        [HttpPost("{id}/status/closing")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(StartDepotClosingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> StartClosing(int id)
        {
            var command = new StartDepotClosingCommand(id, GetUserId());
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách toàn bộ trạng thái kho.</summary>
        [HttpGet("metadata/depot-statuses")]
        public async Task<IActionResult> GetDepotStatuses()
        {
            var result = await _mediator.Send(new GetDepotStatusMetadataQuery());
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách trạng thái có thể set qua PATCH /{id}/status (Available / Unavailable).</summary>
        [HttpGet("metadata/changeable-statuses")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public IActionResult GetChangeableStatuses()
        {
            var result = new List<MetadataDto>
            {
                new() { Key = DepotStatus.Available.ToString(),   Value = "Đang hoạt động" },
                new() { Key = DepotStatus.Unavailable.ToString(), Value = "Ngưng hoạt động" }
            };
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách kho dùng cho dropdown (key = id, value = tên).</summary>
        [HttpGet("metadata/depots")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDepotMetadata()
        {
            var result = await _mediator.Send(new GetDepotMetadataQuery());
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách kho đang hoạt động (trừ kho Closed, Unavailable, và Kho đã đầy) dùng cho dropdown.</summary>
        [HttpGet("metadata/active-depots")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveDepotMetadata()
        {
            var result = await _mediator.Send(new GetDepotMetadataQuery { ExcludeUnavailableAndClosed = true });
            return Ok(result);
        }

        /// <summary>[Manager] Lấy danh sách các kho mà tài khoản hiện tại đang quản lý (hỗ trợ nhiều kho/người).</summary>
        [HttpGet("metadata/my-managed-depots")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(List<ManagedDepotDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyManagedDepots()
        {
            var userId = GetUserId();
            var result = await _managerDepotAccessService.GetManagedDepotsAsync(userId);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách Manager (RoleId=4) dùng cho dropdown gán manager. Loại trừ account bị ban.
        /// Nếu truyền depotId, loại trừ manager đang active trong kho đó.</summary>
        [HttpGet("metadata/available-managers")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(List<AvailableManagerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableManagers([FromQuery] int? depotId = null)
        {
            var result = await _mediator.Send(new GetAvailableManagersMetadataQuery(depotId));
            return Ok(result);
        }

        /// <summary>[Admin] Danh sách manager đang active trong kho. Dùng để frontend hiển thị trước khi gán/gỡ quản kho.</summary>
        [HttpGet("{id}/managers")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(List<DepotManagerInfoDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDepotManagers(int id)
        {
            var result = await _mediator.Send(new GetDepotManagersQuery(id));
            return Ok(result);
        }

        /// <summary>[Manager] Xem quỹ kho của mình.</summary>
        [HttpGet("my-fund")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(DepotFundDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyFund([FromQuery] int depotId)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyDepotFundQuery(userId, depotId));
            return Ok(result);
        }

        /// <summary>[Admin] Xem quỹ tất cả kho (có phân trang).</summary>
        [HttpGet("funds")]
        [Authorize(Policy = PermissionConstants.SystemConfigManage)]
        [ProducesResponseType(typeof(PagedResult<DepotFundsResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllFunds(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await _mediator.Send(new GetAllDepotFundsQuery(pageNumber, pageSize, search));
            return Ok(result);
        }

        // ─── Depot Closure endpoints ──────────────────────────────────────────

        /// <summary>
        /// [Manager kho đích] Tự khám phá phiên nhận hàng từ kho nguồn đang đóng cửa.
        /// Không cần truyền bất kỳ ID nào — hệ thống tự xác định kho từ token.
        /// Trả về đủ SourceDepotId + ClosureId + TransferId để gọi các action tiếp theo.
        /// Trả 204 nếu hiện không có phiên chuyển hàng nào đang chờ.
        /// </summary>
        [HttpGet("my-incoming-closure-transfer")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(MyIncomingClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyIncomingClosureTransfer([FromQuery] int depotId)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyIncomingClosureTransferQuery(userId, depotId));
            return result is null ? NoContent() : Ok(result);
        }

        /// <summary>[Admin] Lấy toàn bộ lịch sử phiên đóng kho của một kho.</summary>
        [HttpGet("{id}/closures")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(List<DepotClosureDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosures(int id)
        {
            var result = await _mediator.Send(new GetDepotClosuresQuery(id));
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Alias tương thích ngược cho FE cũ: lấy lịch sử phiên đóng kho bằng query string `?depotId=...`.
        /// Route chuẩn hiện tại là GET /logistics/depot/{id}/closures.
        /// </summary>
        [HttpGet("closures")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(List<DepotClosureDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosuresLegacy([FromQuery] int depotId)
        {
            return await GetClosures(depotId);
        }

        /// <summary>
        /// [Manager/Admin] Lấy danh sách transfer liên quan đến kho đang đóng hoặc nhận hàng đóng kho.
        /// Hỗ trợ FE cũ gọi theo dạng query string `?depotId=...`.
        /// </summary>
        [HttpGet("transfer")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(List<MyClosureTransferDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyClosureTransfers([FromQuery] int? depotId = null)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyClosureTransfersQuery(userId, depotId));
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách resolutionType cho quy trình đóng kho (TransferToDepot / ExternalResolution).</summary>
        [HttpGet("metadata/closure")]
        [ProducesResponseType(typeof(DepotClosureMetadataResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClosureMetadata()
        {
            var result = await _mediator.Send(new GetDepotClosureMetadataQuery());
            return Ok(result);
        }

        /// <summary>
        /// Xác nhận đóng kho hoàn toàn.
        /// - Kho phải ở trạng thái Closing.
        /// - Kho trống → đóng ngay (200 OK).
        /// - Còn hàng → 409 Conflict kèm danh sách hàng tồn.
        ///   Sau đó tiếp tục xử lý tồn kho theo 1 trong 2 cách:
        ///   (1) Xử lý bên ngoài: GET /{id}/close/export-template → POST /{id}/close/external-resolution.
        ///   (2) Chuyển kho: POST /{id}/close/transfer { targetDepotId, reason }.
        /// </summary>
        [HttpPost("{id}/closed")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(InitiateDepotClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InitiateDepotClosureResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloseDepot(int id, [FromBody] InitiateDepotClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new InitiateDepotClosureCommand(id, userId, dto.Reason);
            var result = await _mediator.Send(command);

            if (result.Success)
            {
                await _operationalHubService.PushDepotClosureUpdateAsync(
                    new DepotClosureRealtimeUpdate
                    {
                        SourceDepotId = id,
                        ClosureId = result.ClosureId,
                        EntityType = "Closure",
                        Action = "ClosedConfirmed",
                        Status = "Completed"
                    },
                    HttpContext.RequestAborted);

                await _operationalHubService.PushDepotInventoryUpdateAsync(id, "DepotClosed", HttpContext.RequestAborted);
                await _operationalHubService.PushLogisticsUpdateAsync("depots", cancellationToken: HttpContext.RequestAborted);
            }

            return result.Success ? Ok(result) : Conflict(result);
        }

        /// <summary>
        /// Legacy alias cho endpoint xác nhận đóng kho.
        /// </summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{id}/close")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        public async Task<IActionResult> CloseDepotLegacy(int id, [FromBody] InitiateDepotClosureRequestDto dto)
        {
            return await CloseDepot(id, dto);
        }

        /// <summary>
        /// [Admin] Gợi ý phương án phân bổ hàng tồn sang các kho đích trước khi chốt chuyển kho đóng.
        /// </summary>
        [HttpGet("{id}/close/transfer-suggestions")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(ClosureTransferSuggestionsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosureTransferSuggestions(int id)
        {
            var result = await _mediator.Send(new GetClosureTransferSuggestionsQuery
            {
                DepotId = id
            });

            return Ok(result);
        }

        /// <summary>
        /// [Admin] Tải file Excel template liệt kê hàng tồn kho — depot manager điền cách xử lý rồi upload lại.
        /// Kho phải ở trạng thái Closing và còn hàng.
        /// </summary>
        [HttpGet("{id}/close/export-template")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ExportClosureTemplate(int id)
        {
            var userId = GetUserId();
            var query = new ExportClosureTemplateQuery(userId, id);
            var result = await _mediator.Send(query);
            return File(result.FileContent, result.ContentType, result.FileName);
        }

        /// <summary>
        /// [Admin] Đánh dấu và thông báo cho manager kho để họ xử lý bên ngoài.
        /// Kho phải ở trạng thái Closing và phiên đóng kho đang chờ chọn hình thức xử lý.
        /// </summary>
        [HttpPost("{id}/close/mark-external")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(MarkExternalClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(MarkExternalClosureResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkExternalClosure(int id, [FromBody] MarkExternalClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new MarkExternalClosureCommand(id, userId, dto.Reason);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Gửi kết quả xử lý tồn kho bên ngoài dạng JSON.
        /// Frontend convert file Excel template thành JSON rồi gửi lên.
        /// Kho phải ở trạng thái Closing và còn hàng (không có transfer đang diễn ra).
        /// Server tạo bản ghi kiểm toán nội bộ, xoá toàn bộ inventory, và đóng kho.
        /// </summary>
        [HttpPost("close/external-resolution")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(UploadExternalResolutionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UploadExternalResolution([FromBody] UploadExternalResolutionRequestDto dto, [FromQuery] int depotId)
        {
            var userId = GetUserId();
            var command = new UploadExternalResolutionCommand(userId, dto.Items, depotId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Khởi tạo quy trình chuyển toàn bộ hàng tồn sang kho khác để đóng kho nguồn.
        /// Kho nguồn phải ở trạng thái Closing và còn hàng.
        /// Trả về transferId — dùng cho tất cả các bước tiếp theo (prepare → ship → complete → receive).
        /// </summary>
        [HttpPost("{id}/close/transfer")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(InitiateDepotClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> InitiateClosureTransfer(int id, [FromBody] InitiateDepotClosureTransferRequestDto dto)
        {
            var userId = GetUserId();
            var assignments = dto.Assignments
                .Select(a => new InitiateDepotClosureTransferDepotAssignmentCommandItem(
                    a.TargetDepotId,
                    a.Items.Select(i => new InitiateDepotClosureTransferAssignmentCommandItem(
                        i.ItemModelId, i.ItemType, i.Quantity)).ToList()))
                .ToList();
            var command = new InitiateDepotClosureTransferCommand(id, userId, dto.Reason, assignments);
            var result = await _mediator.Send(command);

            foreach (var transfer in result.Transfers)
            {
                await _operationalHubService.PushDepotClosureUpdateAsync(
                    new DepotClosureRealtimeUpdate
                    {
                        SourceDepotId = id,
                        TargetDepotId = transfer.TargetDepotId,
                        ClosureId = result.ClosureId,
                        TransferId = transfer.TransferId,
                        EntityType = "Transfer",
                        Action = "TransferPlanned",
                        Status = transfer.TransferStatus
                    },
                    HttpContext.RequestAborted);
            }

            return Ok(result);
        }

        /// <summary>
        /// [Admin] Huỷ transfer đang chờ xử lý — kho nguồn giữ trạng thái Closing.
        /// Chỉ huỷ được khi transfer ở trạng thái Initiated hoặc Preparing.
        /// </summary>
        [HttpDelete("{id}/close/transfer/{transferId}")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(CancelDepotClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelClosureTransfer(int id, int transferId, [FromBody] CancelDepotClosureTransferRequestDto dto)
        {
            var userId = GetUserId();
            var command = new CancelDepotClosureTransferCommand(id, transferId, userId, dto.Reason);
            var result = await _mediator.Send(command);

            await _operationalHubService.PushDepotClosureUpdateAsync(
                new DepotClosureRealtimeUpdate
                {
                    SourceDepotId = id,
                    TransferId = transferId,
                    EntityType = "Transfer",
                    Action = "Cancelled",
                    Status = result.TransferStatus
                },
                HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// [Admin] Huỷ quy trình đóng kho đang chờ xử lý (legacy — dùng DELETE /{id}/close/transfer/{transferId}).
        /// </summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("{id}/close/{closureId}")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(CancelDepotClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelClosure(int id, int closureId, [FromBody] CancelDepotClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new CancelDepotClosureCommand(id, closureId, userId, dto.CancellationReason);
            var result = await _mediator.Send(command);

            await _operationalHubService.PushDepotClosureUpdateAsync(
                new DepotClosureRealtimeUpdate
                {
                    SourceDepotId = id,
                    ClosureId = closureId,
                    EntityType = "Closure",
                    Action = "Cancelled",
                    Status = "Cancelled"
                },
                HttpContext.RequestAborted);

            await _operationalHubService.PushDepotInventoryUpdateAsync(id, "ClosureCancelled", HttpContext.RequestAborted);
            await _operationalHubService.PushLogisticsUpdateAsync("depots", cancellationToken: HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// [Manager kho nguồn] Xác nhận đang chuẩn bị hàng — chuyển transfer sang Preparing.
        /// {id} là kho nguồn. Dùng transferId từ POST /{id}/close/transfer.
        /// </summary>
        [HttpPost("{id}/transfer/{transferId}/prepare")]
        [HttpPost("{id}/close/transfer/{transferId}/prepare")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(PrepareClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> PrepareClosureTransfer(
            int id, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new PrepareClosureTransferCommand(transferId, userId, dto.Note, id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho nguồn] Xác nhận đã xuất hàng — chuyển transfer sang Shipping.
        /// {id} là kho nguồn. Dùng transferId từ POST /{id}/close/transfer.
        /// </summary>
        [HttpPost("{id}/transfer/{transferId}/ship")]
        [HttpPost("{id}/close/transfer/{transferId}/ship")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(ShipClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipClosureTransfer(
            int id, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ShipClosureTransferCommand(transferId, userId, dto.Note, id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho nguồn] Xác nhận đã giao toàn bộ hàng — chuyển transfer sang Completed.
        /// {id} là kho nguồn. Dùng transferId từ POST /{id}/close/transfer.
        /// </summary>
        [HttpPost("{id}/transfer/{transferId}/complete")]
        [HttpPost("{id}/close/transfer/{transferId}/complete")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(CompleteClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CompleteClosureTransfer(
            int id, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new CompleteClosureTransferCommand(transferId, userId, dto.Note, id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho đích] Xác nhận đã nhận hàng — kích hoạt bulk transfer inventory và hoàn tất đóng kho nguồn.
        /// {id} là kho ĐÍCH (target depot). Dùng transferId từ GET /my-incoming-closure-transfer.
        /// </summary>
        [HttpPost("{id}/transfer/{transferId}/receive")]
        [HttpPost("{id}/close/transfer/{transferId}/receive")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(ReceiveClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReceiveClosureTransfer(
            int id, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ReceiveClosureTransferCommand(transferId, userId, dto.Note, id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Xem trạng thái bản ghi chuyển hàng khi đóng kho.
        /// {id} là kho nguồn (source depot). Manager kho nguồn / kho đích đều dùng được.
        /// </summary>
        [HttpGet("{id}/transfer/{transferId}")]
        [HttpGet("{id}/close/transfer/{transferId}")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(ClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosureTransfer(int id, int transferId)
        {
            var userId = GetUserId();
            var query = new GetClosureTransferQuery(id, transferId, userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }
            throw new Application.Exceptions.UnauthorizedException("Token không hợp lệ hoặc thiếu thông tin người dùng.");
        }

        // ─────────────────── CHART ENDPOINTS ───────────────────

        /// <summary>
        /// [Chart 1] Biểu đồ tiến trình sức chứa kho (thể tích &amp; cân nặng hiện tại / tối đa).
        /// Dùng cho progress chart.
        /// </summary>
        [HttpGet("{id}/chart/capacity")]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        [ProducesResponseType(typeof(RESQ.Application.UseCases.Logistics.Queries.GetDepotCapacityChart.DepotCapacityChartDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCapacityChart(int id)
        {
            var result = await _mediator.Send(
                new RESQ.Application.UseCases.Logistics.Queries.GetDepotCapacityChart.GetDepotCapacityChartQuery(id));
            return Ok(result);
        }

        /// <summary>
        /// [Chart 2] Biểu đồ biến động kho (số lượng nhập/xuất theo ngày) – line chart.
        /// </summary>
        [HttpGet("{id}/chart/inventory-movement")]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        [ProducesResponseType(typeof(RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart.DepotInventoryMovementChartDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInventoryMovementChart(
            int id,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to   = null)
        {
            var result = await _mediator.Send(
                new RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryMovementChart.GetDepotInventoryMovementChartQuery
                {
                    DepotId = id,
                    From    = from,
                    To      = to
                });
            return Ok(result);
        }
    }
}
