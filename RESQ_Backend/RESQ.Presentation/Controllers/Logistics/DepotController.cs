using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
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
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
using RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;
using RESQ.Application.UseCases.Logistics.Commands.MarkExternalClosure;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Logistics.Queries.GetAvailableManagersMetadata;
using RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotById;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotsByCluster;
using RESQ.Application.UseCases.Logistics.Queries.GetMyClosureTransfers;
using RESQ.Application.UseCases.Logistics.Queries.GetMyIncomingClosureTransfer;
using RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot")]
    [ApiController]
    public class DepotController(
        IMediator mediator,
        IUserRepository userRepository,
        IDepotInventoryRepository depotInventoryRepository) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

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

        /// <summary>Tạo kho mới. Manager là optional - nếu gán ngay thì kho chuyển sang Available.</summary>
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
                dto.ImageUrl
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
                dto.ImageUrl
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Gán / đổi manager cho kho. Nếu kho đang có manager khác, manager cũ sẽ được unassign tự động.
        /// Kho chuyển sang trạng thái Available sau khi gán.
        /// </summary>
        [HttpPatch("{id}/manager")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(AssignDepotManagerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignManager(int id, [FromBody] AssignDepotManagerRequestDto dto)
        {
            var command = new AssignDepotManagerCommand(id, dto.ManagerId);
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
            var command = new UnassignDepotManagerCommand(id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Thay d?i tr?ng th�i kho (Available / Unavailable). Admin c� th? d?i b?t k? kho, Manager ch? d?i kho m�nh qu?n l�.</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        public async Task<IActionResult> ChangeStatus(int id, [FromQuery] ChangeDepotStatusRequestDto dto)
        {
            var depotStatus = dto.Status == ChangeableDepotStatus.Available ? DepotStatus.Available : (dto.Status == ChangeableDepotStatus.Closing ? DepotStatus.Closing : DepotStatus.Unavailable);
            var command = new ChangeDepotStatusCommand(id, depotStatus, GetUserId());
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

        /// <summary>[Metadata] Danh s�ch tr?ng th�i c� th? set qua PATCH /{id}/status (Available / Unavailable).</summary>
        [HttpGet("metadata/changeable-statuses")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public IActionResult GetChangeableStatuses()
        {
            var result = new List<MetadataDto>
            {
                new() { Key = DepotStatus.Available.ToString(),   Value = "Đang hoạt động" },
                new() { Key = DepotStatus.Unavailable.ToString(), Value = "Tạm ngưng hoạt động" },
                new() { Key = DepotStatus.Closing.ToString(), Value = "Đang đóng kho" }
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

        /// <summary>[Metadata] Danh sách kho đang hoạt động (trừ kho Closed và Unavailable) dùng cho dropdown.</summary>
        [HttpGet("metadata/active-depots")]
        [ProducesResponseType(typeof(List<MetadataDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveDepotMetadata()
        {
            var result = await _mediator.Send(new GetDepotMetadataQuery { ExcludeUnavailableAndClosed = true });
            return Ok(result);
        }

        /// <summary>
        /// [Metadata] Danh s�ch Manager (RoleId=4) chua qu?n l� kho n�o � d�ng cho dropdown g�n manager.
        /// </summary>
        [HttpGet("metadata/available-managers")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(List<AvailableManagerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableManagers()
        {
            var result = await _mediator.Send(new GetAvailableManagersMetadataQuery());
            return Ok(result);
        }

        /// <summary>[Manager] Xem quỹ kho của mình.</summary>
        [HttpGet("my-fund")]
        [Authorize(Policy = PermissionConstants.InventoryDepotManage)]
        [ProducesResponseType(typeof(DepotFundDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyFund()
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyDepotFundQuery(userId));
            return Ok(result);
        }

        /// <summary>[Admin] Xem quỹ tất cả kho.</summary>

        [HttpGet("funds")]
    [Authorize(Policy = PermissionConstants.SystemConfigManage)]
        [ProducesResponseType(typeof(List<DepotFundsResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllFunds()
        {
            var result = await _mediator.Send(new GetAllDepotFundsQuery());
            return Ok(result);
        }

        // --- Depot Closure endpoints ------------------------------------------

        /// <summary>
        /// [Manager kho d�ch] T? kh�m ph� phi�n nh?n h�ng t? kho ngu?n dang d�ng c?a.
        /// Kh�ng c?n truy?n b?t k? ID n�o � h? th?ng t? x�c d?nh kho t? token.
        /// Tr? v? d? SourceDepotId + ClosureId + TransferId d? g?i c�c action ti?p theo.
        /// Trả 204 nếu hiện không có phiên chuyển hàng nào đang chờ.
        /// </summary>
        [HttpGet("my-incoming-closure-transfer")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(MyIncomingClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyIncomingClosureTransfer()
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyIncomingClosureTransferQuery(userId));
            return result is null ? NoContent() : Ok(result);
        }

        /// <summary>[Manager] Lay toan bo lich su phien dong kho cua kho dang quan ly. DepotId duoc suy ra tu token.</summary>
        [HttpGet("closures")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(List<DepotClosureDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyClosures()
        {
            var userId = GetUserId();
            var depotId = await GetRequiredManagerDepotIdAsync(userId);
            var result = await _mediator.Send(new GetDepotClosuresQuery(depotId, userId));
            return Ok(result);
        }

        /// <summary>[Admin] Lay toan bo lich su phien dong kho cua mot kho cu the.</summary>
        [HttpGet("{depotId}/closures")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(List<DepotClosureDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosuresByDepot(int depotId)
        {
            var userId = GetUserId();
            await EnsureAdminRoleAsync(userId);
            var result = await _mediator.Send(new GetDepotClosuresQuery(depotId, userId));
            return Ok(result);
        }

        /// <summary>[Manager] Xem chi tiet mot phien dong kho cua kho dang quan ly. DepotId duoc suy ra tu token.</summary>
        [HttpGet("closures/{closureId}")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(DepotClosureDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyClosureDetail(int closureId)
        {
            var userId = GetUserId();
            var depotId = await GetRequiredManagerDepotIdAsync(userId);
            var result = await _mediator.Send(new GetDepotClosureDetailQuery(depotId, closureId, userId));
            return Ok(result);
        }

        /// <summary>[Admin] Xem chi tiết một phiên đóng kho của một kho cụ thể.</summary>
        [HttpGet("{depotId}/closures/{closureId}")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(DepotClosureDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosureDetailByDepot(int depotId, int closureId)
        {
            var userId = GetUserId();
            await EnsureAdminRoleAsync(userId);
            var result = await _mediator.Send(new GetDepotClosureDetailQuery(depotId, closureId, userId));
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho ngu?n/kho d�ch] L?y to�n b? transfer d�ng kho m� kho hi?n t?i c� tham gia.
        /// H? th?ng t? x�c d?nh depotId t? token v� tr? v? c? transfer ph�a ngu?n l?n ph�a d�ch.
        /// </summary>
        [HttpGet("transfer")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(List<MyClosureTransferDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyClosureTransfers()
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetMyClosureTransfersQuery(userId));
            return Ok(result);
        }

        /// <summary>[Metadata] Danh s�ch resolutionType cho quy tr�nh d�ng kho (TransferToDepot / ExternalResolution).</summary>
        [HttpGet("metadata/closure")]
        [ProducesResponseType(typeof(DepotClosureMetadataResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClosureMetadata()
        {
            var result = await _mediator.Send(new GetDepotClosureMetadataQuery());
            return Ok(result);
        }

        /// <summary>
        /// [Admin] ��ng kho � bu?c kh?i t?o.
        /// - Kho ph?i ? tr?ng th�i Unavailable.
        /// - Kho tr?ng v� chua c� phi�n x? l� h�ng t?n tru?c d� ? d�ng ngay (200 OK).
        /// - Kho d� du?c x? l� h�ng t?n xong t? tru?c (external resolution ho?c transfer receive), admin g?i l?i endpoint n�y d? finalize:
        ///   chuy?n qu? kho v? qu? h? th?ng, chuy?n kho sang Closed v� g? manager kh?i kho.
        /// - C�n h�ng ? 409 Conflict k�m danh s�ch h�ng t?n.
        ///   Admin ch?n 1 trong 2 c�ch x? l�:
        ///   (1) X? l� b�n ngo�i: GET /close/export-template ? POST /close/external-resolution.
        ///   (2) Chuy?n kho: POST /{id}/close/transfer { targetDepotId, reason }.
        /// </summary>
        [HttpPost("{id}/close")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(InitiateDepotClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InitiateDepotClosureResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloseDepot(int id, [FromBody] InitiateDepotClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new InitiateDepotClosureCommand(id, userId, dto.Reason);
            var result = await _mediator.Send(command);
            return result.Success ? Ok(result) : Conflict(result);
        }

        /// <summary>
        /// [Manager] T?i file Excel template li?t k� h�ng t?n kho c?a kho m�nh dang qu?n l�.
        /// H? th?ng t? l?y depotId t? token ngu?i d�ng qua b?ng depot manager v?i di?u ki?n UnassignedAt == null.
        /// Kho ch? c?n c�n h�ng t?n d? xu?t m?u x? l�.
        /// </summary>
        [HttpGet("close/export-template")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ExportClosureTemplate()
        {
            var userId = GetUserId();
            var query = new ExportClosureTemplateQuery(userId);
            var result = await _mediator.Send(query);
            return File(result.FileContent, result.ContentType, result.FileName);
        }

        /// <summary>
        /// [Admin] ��nh d?u v� th�ng b�o cho manager kho d? h? x? l� b�n ngo�i.
        /// Kho ph?i ? tr?ng th�i Unavailable v� dang c� phi�n d�ng kho du?c t?o (chua x�c d?nh h�nh th?c).
        /// </summary>
        [HttpPost("{id}/close/mark-external")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(MarkExternalClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkExternalClosure(int id, [FromBody] MarkExternalClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new MarkExternalClosureCommand(id, userId, dto.Reason);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] G?i k?t qu? x? l� t?n kho b�n ngo�i d?ng JSON.
        /// Frontend convert file Excel template th�nh JSON r?i g?i l�n.
        /// Kho ph?i ? tr?ng th�i Unavailable v� c�n h�ng (kh�ng c� transfer dang di?n ra).
        /// Server t?o b?n ghi ki?m to�n n?i b? v� xo� to�n b? inventory.
        /// Sau bu?c n�y kho v?n gi? tr?ng th�i Unavailable; admin ph?i g?i l?i POST /{id}/close d? finalize d�ng kho.
        /// </summary>
        [HttpPost("close/external-resolution")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(UploadExternalResolutionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UploadExternalResolution([FromBody] UploadExternalResolutionRequestDto dto)
        {
            var userId = GetUserId();
            var command = new UploadExternalResolutionCommand(userId, dto.Items);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Kh?i t?o quy tr�nh chuy?n to�n b? h�ng t?n sang kho kh�c d? d�ng kho ngu?n.
        /// Kho ngu?n ph?i ? tr?ng th�i Unavailable v� c�n h�ng.
        /// Tr? v? transferId � d�ng cho t?t c? c�c bu?c ti?p theo (prepare ? ship ? complete ? receive).
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
            var command = new InitiateDepotClosureTransferCommand(
    id,
    userId,
    dto.Reason,
    dto.Assignments.Select(a => new InitiateDepotClosureTransferDepotAssignmentCommandItem(
        a.TargetDepotId,
        a.Items.Select(i => new InitiateDepotClosureTransferAssignmentCommandItem(
            i.ItemModelId,
            i.ItemType,
            i.Quantity)).ToList()
    )).ToList()
);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Hu? transfer dang ch? x? l� � kho ngu?n gi? tr?ng th�i Unavailable.
        /// Ch? hu? du?c khi transfer ? tr?ng th�i Initiated ho?c Preparing.
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
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Hu? quy tr�nh d�ng kho dang ch? x? l� (legacy � d�ng DELETE /{id}/close/transfer/{transferId}).
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
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho ngu?n] X�c nh?n dang chu?n b? h�ng � chuy?n transfer sang Preparing.
        /// D�ng transferId t? POST /close/transfer.
        /// </summary>
        [HttpPost("transfer/{transferId}/prepare")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(PrepareClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> PrepareClosureTransfer(
            int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new PrepareClosureTransferCommand(transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho ngu?n] X�c nh?n d� xu?t h�ng � chuy?n transfer sang Shipping.
        /// D�ng transferId t? POST /close/transfer.
        /// </summary>
        [HttpPost("transfer/{transferId}/ship")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(ShipClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipClosureTransfer(
            int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ShipClosureTransferCommand(transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho ngu?n] X�c nh?n d� giao to�n b? h�ng � chuy?n transfer sang Completed.
        /// D�ng transferId t? POST /close/transfer.
        /// </summary>
        [HttpPost("transfer/{transferId}/complete")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(CompleteClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CompleteClosureTransfer(
            int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new CompleteClosureTransferCommand(transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho d�ch] X�c nh?n d� nh?n h�ng � k�ch ho?t bulk transfer inventory v� ho�n t?t bu?c x? l� h�ng t?n.
        /// Sau bu?c n�y kho ngu?n v?n gi? tr?ng th�i Unavailable; admin ph?i g?i l?i POST /{id}/close d? finalize d�ng kho.
        /// D�ng transferId t? GET /my-incoming-closure-transfer.
        /// </summary>
        [HttpPost("transfer/{transferId}/receive")]
        [Authorize(Policy = PermissionConstants.PolicyInventoryWrite)]
        [ProducesResponseType(typeof(ReceiveClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReceiveClosureTransfer(
            int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ReceiveClosureTransferCommand(transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>L?y UserId t? access token hi?n t?i.</summary>
        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }
            throw new Application.Exceptions.UnauthorizedException("Token kh�ng h?p l? ho?c thi?u th�ng tin ngu?i d�ng.");
        }

        private async Task<int> GetRequiredManagerDepotIdAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedException("Kh�ng t�m th?y th�ng tin ngu?i d�ng t? token.");

            if (user.RoleId != 4)
                throw new ForbiddenException("Endpoint nay chi danh cho manager kho.");

            return await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(userId)
                ?? throw ExceptionCodes.WithCode(
                    new NotFoundException("Ban hien kh�ng ph? tr�ch kho n�o."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
        }

        private async Task EnsureAdminRoleAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedException("Không tìm thấy thông tin người dùng từ token.");

            if (user.RoleId != 1)
                throw new ForbiddenException("Endpoint này chỉ dành cho admin.");
        }

        /// <summary>
        /// [Admin/Manager] Phân tích sức chứa các kho còn lại và tự động đề xuất phân bổ hàng tồn kho của kho đang đóng.
        /// API kết hợp tính toán remaining capacity (thể tích/cân nặng) của kho target và thuật toán greedy để chia mảng.
        /// Front-end có thể dùng list TargetDepots để hiển thị không gian trống, và SuggestedTransfers để fill vào form phân bổ trước khi submit.
        /// </summary>
        [HttpGet("{id}/close/transfer-suggestions")]
        [Authorize(Policy = PermissionConstants.PolicyDepotView)]
        [ProducesResponseType(typeof(RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions.ClosureTransferSuggestionsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClosureTransferSuggestions(int id)
        {
            var query = new RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions.GetClosureTransferSuggestionsQuery { DepotId = id };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}