using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;
using RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;
using RESQ.Application.UseCases.Personnel.Commands.StartGathering;
using RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Presentation.Controllers.Personnel
{
    [Route("personnel/assembly-point")]
    [ApiController]
    public class AssemblyPointsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách điểm tập kết có phân trang.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var query = new GetAllAssemblyPointsQuery { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách trạng thái điểm tập kết dùng cho dropdown.</summary>
        [HttpGet("status-metadata")]
        public async Task<IActionResult> GetStatusMetadata()
        {
            var query = new GetAssemblyPointStatusMetadataQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Xem chi tiết điểm tập kết theo ID.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var query = new GetAssemblyPointByIdQuery(id);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>[Metadata] Danh sách điểm tập kết dùng cho dropdown (key = id, value = tên).</summary>
        [HttpGet("metadata")]
        public async Task<IActionResult> GetMetadata()
        {
            var query = new GetAssemblyPointMetadataQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Lấy danh sách rescuer thuộc điểm tập kết.</summary>
        [HttpGet("{id}/rescuers")]
        public async Task<IActionResult> GetRescuersByAssemblyPoint(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new GetRescuersByAssemblyPointQuery(id, pageNumber, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Lấy danh sách sự kiện tập trung theo điểm tập kết (phân trang).</summary>
        [HttpGet("{id}/events")]
        public async Task<IActionResult> GetAssemblyEvents(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new GetAssemblyEventsQuery(id, pageNumber, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Tạo điểm tập kết mới.</summary>
        [HttpPost]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Create([FromBody] CreateAssemblyPointRequestDto dto)
        {
            var command = new CreateAssemblyPointCommand(
                dto.Name,
                dto.Latitude,
                dto.Longitude,
                dto.MaxCapacity
            );

            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>Cập nhật điểm tập kết (Code không thay đổi).</summary>
        [HttpPut("{id}")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAssemblyPointRequestDto dto)
        {
            var command = new UpdateAssemblyPointCommand(
                id,
                dto.Name,
                dto.Latitude,
                dto.Longitude,
                dto.MaxCapacity
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Thay đổi trạng thái điểm tập kết.</summary>
        [HttpPatch("{id}/status")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeAssemblyPointStatusRequestDto dto)
        {
            var command = new ChangeAssemblyPointStatusCommand(id, dto.Status);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xóa điểm tập kết.</summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Delete(int id)
        {
            var command = new DeleteAssemblyPointCommand(id);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Gán rescuer vào điểm tập kết (hoặc gỡ nếu assemblyPointId = null).</summary>
        [HttpPut("rescuers/{userId}/assignment")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> AssignRescuer(Guid userId, [FromBody] AssignRescuerToAssemblyPointRequestDto dto)
        {
            var command = new AssignRescuerToAssemblyPointCommand(userId, dto.AssemblyPointId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Lên lịch tập trung tại điểm tập kết → tạo AssemblyEvent + gán participant + gửi Firebase.</summary>
        [HttpPost("{id}/schedule-gathering")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> ScheduleGathering(int id, [FromBody] ScheduleGatheringRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var createdBy))
                return Unauthorized();

            var command = new ScheduleGatheringCommand(id, dto.AssemblyDate, createdBy);
            var eventId = await _mediator.Send(command);
            return Ok(new { EventId = eventId });
        }

        // [DEPRECATED] Không cần dùng nữa — event được tạo trực tiếp ở trạng thái Gathering khi schedule-gathering.
        // /// <summary>Mở check-in cho sự kiện tập trung (Scheduled → Gathering).</summary>
        // [HttpPost("events/{eventId}/start-gathering")]
        // [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        // public async Task<IActionResult> StartGathering(int eventId)
        // {
        //     var command = new StartGatheringCommand(eventId);
        //     await _mediator.Send(command);
        //     return NoContent();
        // }

        /// <summary>Rescuer check-in tại sự kiện tập trung (kèm GPS validation trong bán kính 200m).</summary>
        [HttpPost("events/{eventId}/check-in")]
        [Authorize]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] CheckInRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var command = new CheckInAtAssemblyPointCommand(eventId, userId, dto.Latitude, dto.Longitude);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Lấy danh sách rescuer đã check-in tại sự kiện tập trung (để coordinator chia team).</summary>
        /// <summary><paramref name="search"/>: tìm kiếm theo firstName, lastName, phone hoặc email (OR).</summary>
        [HttpGet("events/{eventId}/checked-in-rescuers")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> GetCheckedInRescuers(
            int eventId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] RescuerType? rescuerType = null,
            [FromQuery] string? abilitySubgroupCode = null,
            [FromQuery] string? abilityCategoryCode = null,
            [FromQuery] string? search = null)
        {
            var query = new GetCheckedInRescuersQuery(eventId, pageNumber, pageSize, rescuerType, abilitySubgroupCode, abilityCategoryCode, search);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Rescuer xem danh sách lịch triệu tập của chính mình (sự kiện đã/đang được gán).</summary>
        [HttpGet("events/my")]
        [Authorize]
        public async Task<IActionResult> GetMyAssemblyEvents(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var query = new GetMyAssemblyEventsQuery(userId, pageNumber, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}
