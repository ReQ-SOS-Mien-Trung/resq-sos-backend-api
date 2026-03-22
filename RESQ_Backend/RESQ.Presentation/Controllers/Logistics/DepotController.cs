using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetAllDepotFunds;
using RESQ.Application.UseCases.Finance.Queries.GetMyDepotFund;
using RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;
using RESQ.Application.UseCases.Logistics.Commands.CreateDepot;
using RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;
using RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;
using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotById;
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
        public async Task<IActionResult> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllDepotsQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize
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

        /// <summary>Tạo kho mới.</summary>
        [HttpPost]
        [Authorize(Policy = PermissionConstants.InventoryGlobalManage)]
        public async Task<IActionResult> Create([FromBody] CreateDepotRequestDto dto)
        {
            // Pass primitives to command. GeoLocation creation moved to Handler.
            var command = new CreateDepotCommand(
                dto.Name,
                dto.Address,
                dto.Latitude,
                dto.Longitude,
                dto.Capacity
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
