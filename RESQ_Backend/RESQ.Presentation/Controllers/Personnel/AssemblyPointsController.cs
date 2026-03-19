using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;
using RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.AssignTeamsToAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;
using RESQ.Application.UseCases.Personnel.Commands.StartGathering;
using RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;

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

        /// <summary>Lấy danh sách rescuer thuộc các đội tại một điểm tập kết.</summary>
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

        /// <summary>Gán danh sách đội cứu hộ (chứa rescuer) vào điểm tập kết.</summary>
        [HttpPost("{id}/teams")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> AssignTeams(int id, [FromBody] AssignTeamsToAssemblyPointRequestDto dto)
        {
            var command = new AssignTeamsToAssemblyPointCommand(id, dto.TeamIds);
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

        /// <summary>Mở check-in cho sự kiện tập trung (Scheduled → Gathering).</summary>
        [HttpPost("events/{eventId}/start-gathering")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> StartGathering(int eventId)
        {
            var command = new StartGatheringCommand(eventId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Rescuer check-in tại sự kiện tập trung.</summary>
        [HttpPost("events/{eventId}/check-in")]
        [Authorize]
        public async Task<IActionResult> CheckIn(int eventId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var command = new CheckInAtAssemblyPointCommand(eventId, userId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Lấy danh sách rescuer đã check-in tại sự kiện tập trung (để coordinator chia team).</summary>
        [HttpGet("events/{eventId}/checked-in-rescuers")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> GetCheckedInRescuers(
            int eventId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new GetCheckedInRescuersQuery(eventId, pageNumber, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
    }
}
