using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;
using RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;
using RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosure;
using RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;
using RESQ.Application.UseCases.Logistics.Commands.CompleteClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Commands.PrepareClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.ReceiveClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;
using RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
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
using System.Security.Claims;

namespace RESQ.Presentation.Controllers.Logistics
{
    [Route("logistics/depot")]
    [ApiController]
    public class DepotController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

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
                dto.ManagerId
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
                dto.Capacity
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

        /// <summary>Thay đổi trạng thái kho (Available / Full / Closed / ...).</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        public async Task<IActionResult> ChangeStatus(int id, [FromQuery] ChangeDepotStatusRequestDto dto)
        {
            var command = new ChangeDepotStatusCommand(id, dto.Status);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách trạng thái kho dùng cho dropdown.</summary>
        [HttpGet("metadata/depot-statuses")]
        public async Task<IActionResult> GetDepotStatuses()
        {
            var result = await _mediator.Send(new GetDepotStatusMetadataQuery());
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

        /// <summary>
        /// [Metadata] Danh sách Manager (RoleId=4) chưa quản lý kho nào — dùng cho dropdown gán manager.
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
        [Authorize(Roles = "4")]
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
        [Authorize(Roles = "1")]
        [ProducesResponseType(typeof(List<DepotFundListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllFunds()
        {
            var result = await _mediator.Send(new GetAllDepotFundsQuery());
            return Ok(result);
        }

        // ─── Depot Closure endpoints ──────────────────────────────────────────

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

        /// <summary>[Metadata] Danh sách resolutionType cho quy trình đóng kho.</summary>
        [HttpGet("metadata/closure")]
        [ProducesResponseType(typeof(DepotClosureMetadataResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClosureMetadata()
        {
            var result = await _mediator.Send(new GetDepotClosureMetadataQuery());
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Bắt đầu quy trình đóng kho.
        /// Nếu kho trống → đóng ngay.
        /// Nếu còn hàng → trả về InventorySummary + timeout 30 phút, chờ admin resolve.
        /// </summary>
        [HttpPost("{id}/close/initiate")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(InitiateDepotClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> InitiateClosure(int id, [FromBody] InitiateDepotClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new InitiateDepotClosureCommand(id, userId, dto.Reason);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Xử lý tồn kho trước khi đóng kho.
        /// Option 1: Chuyển toàn bộ hàng sang kho khác (TransferToDepot).
        /// Option 2: Giải quyết bên ngoài — ghi nhận lý do và admin thực hiện (ExternalResolution).
        /// </summary>
        [HttpPost("{id}/close/{closureId}/resolve")]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        [ProducesResponseType(typeof(ResolveDepotClosureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ResolveClosure(int id, int closureId, [FromBody] ResolveDepotClosureRequestDto dto)
        {
            var userId = GetUserId();
            var command = new ResolveDepotClosureCommand(
                id,
                closureId,
                userId,
                dto.ResolutionType,
                dto.TargetDepotId,
                dto.ExternalNote
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Admin] Huỷ quy trình đóng kho đang chờ xử lý.
        /// Trả kho về trạng thái ban đầu (Available / Full).
        /// </summary>
        [HttpPost("{id}/close/{closureId}/cancel")]
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
        /// [Manager kho nguồn] Xác nhận đang chuẩn bị hàng — chuyển transfer sang Preparing.
        /// </summary>
        [HttpPost("{id}/close/{closureId}/transfer/{transferId}/prepare")]
        [Authorize]
        [ProducesResponseType(typeof(PrepareClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> PrepareClosureTransfer(
            int id, int closureId, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new PrepareClosureTransferCommand(id, closureId, transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho nguồn] Xác nhận đã xuất hàng — chuyển transfer sang Shipping.
        /// </summary>
        [HttpPost("{id}/close/{closureId}/transfer/{transferId}/ship")]
        [Authorize]
        [ProducesResponseType(typeof(ShipClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipClosureTransfer(
            int id, int closureId, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ShipClosureTransferCommand(id, closureId, transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho nguồn] Xác nhận đã xuất toàn bộ hàng — chuyển transfer sang Completed.
        /// </summary>
        [HttpPost("{id}/close/{closureId}/transfer/{transferId}/complete")]
        [Authorize]
        [ProducesResponseType(typeof(CompleteClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CompleteClosureTransfer(
            int id, int closureId, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new CompleteClosureTransferCommand(id, closureId, transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// [Manager kho đích] Xác nhận đã nhận hàng — kích hoạt bulk transfer và hoàn tất đóng kho.
        /// </summary>
        [HttpPost("{id}/close/{closureId}/transfer/{transferId}/receive")]
        [Authorize]
        [ProducesResponseType(typeof(ReceiveClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReceiveClosureTransfer(
            int id, int closureId, int transferId,
            [FromBody] ClosureTransferActionDto dto)
        {
            var userId = GetUserId();
            var command = new ReceiveClosureTransferCommand(id, closureId, transferId, userId, dto.Note);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Xem trạng thái bản ghi chuyển hàng khi đóng kho.
        /// Admin truyền depotId (kho nguồn) từ route. Manager kho nguồn / kho đích được tự động xác định depot từ token.
        /// </summary>
        [HttpGet("{id}/close/{closureId}/transfer/{transferId}")]
        [Authorize]
        [ProducesResponseType(typeof(ClosureTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosureTransfer(int id, int closureId, int transferId)
        {
            var userId = GetUserId();
            var query = new GetClosureTransferQuery(id, closureId, transferId, userId);
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
            throw new UnauthorizedAccessException("Invalid User Token");
        }
    }
}
