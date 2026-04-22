using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Personnel.Commands.ActivateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;
using RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointAvailable;
using RESQ.Application.UseCases.Personnel.Commands.CloseAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.AssignRescuerToAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;
using RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.CheckOutAtAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Commands.MarkParticipantAbsent;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;
using RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;
using RESQ.Application.UseCases.Personnel.Queries.GetRescuersByAssemblyPoint;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;
using RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPointCheckInRadius;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCheckInRadius;
using RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPointCheckInRadiusConfigs;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Presentation.Controllers.Personnel
{
    [Route("personnel/assembly-point")]
    [ApiController]
    public class AssemblyPointsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách điểm tập kết có phân trang.</summary>
        /// <param name="pageNumber">Số trang (mặc định 1).</param>
        /// <param name="pageSize">Kích thước trang (mặc định 10).</param>
        /// <param name="status">Lọc theo trạng thái (Created, Available, Unavailable, Closed). Bỏ trống = lấy tất cả.</param>
        [HttpGet]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyPointView)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] AssemblyPointStatus? status = null)
        {
            var query = new GetAllAssemblyPointsQuery { PageNumber = pageNumber, PageSize = pageSize, Status = status };
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
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyPointView)]
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
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyPointView)]
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
                dto.MaxCapacity,
                dto.ImageUrl
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
                dto.MaxCapacity,
                dto.ImageUrl
            );

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Kích hoạt điểm tập kết: <c>Created → Available</c>.
        /// </summary>
        [HttpPatch("{id}/activate")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Activate(int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var changedBy))
                return Unauthorized();

            var result = await _mediator.Send(new ActivateAssemblyPointCommand(id, changedBy));
            return Ok(result);
        }

        /// <summary>
        /// Đánh dấu điểm tập kết không khả dụng: <c>Available → Unavailable</c>.
        /// </summary>
        [HttpPatch("{id}/set-unavailable")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> SetUnavailable(int id, [FromBody] SetAssemblyPointUnavailableRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var changedBy))
                return Unauthorized();

            var result = await _mediator.Send(new SetAssemblyPointUnavailableCommand(id, changedBy, dto.Reason));
            return Ok(result);
        }

        /// <summary>
        /// Khôi phục điểm tập kết về khả dụng: <c>Unavailable → Available</c>.
        /// </summary>
        [HttpPatch("{id}/set-available")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> SetAvailable(int id, [FromBody] SetAssemblyPointAvailableRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var changedBy))
                return Unauthorized();

            var result = await _mediator.Send(new SetAssemblyPointAvailableCommand(id, changedBy, dto.Reason));
            return Ok(result);
        }

        /// <summary>
        /// Đóng vĩnh viễn điểm tập kết: <c>Created → Closed</c> hoặc <c>Unavailable → Closed</c>.
        /// Rescuer được tự động gỡ khỏi điểm tập kết. Yêu cầu bắt buộc phải cung cấp lý do.
        /// Sau khi đóng, không thể thực hiện bất kỳ thao tác nào với điểm tập kết này.
        /// </summary>
        [HttpPatch("{id}/close")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> Close(int id, [FromBody] CloseAssemblyPointRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var changedBy))
                return Unauthorized();

            var result = await _mediator.Send(new CloseAssemblyPointCommand(id, changedBy, dto.Reason));
            return Ok(result);
        }

        /// <summary>Gán rescuer vào điểm tập kết (hoặc gỡ nếu assemblyPointId = null).</summary>
        [HttpPut("rescuers/{userId}/assignment")]
        [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)]
        public async Task<IActionResult> AssignRescuer(Guid userId, [FromBody] AssignRescuerToAssemblyPointRequestDto dto)
        {
            var command = new AssignRescuerToAssemblyPointCommand(userId, dto.AssemblyPointId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Gán một hoặc nhiều rescuer vào cùng một điểm tập kết (bulk).
        /// AssemblyPointId = null → gỡ tất cả khỏi điểm tập kết hiện tại.
        /// </summary>
        [HttpPost("rescuers/assignment")]
        [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)]
        public async Task<IActionResult> BulkAssignRescuers([FromBody] BulkAssignRescuersToAssemblyPointRequestDto dto)
        {
            var command = new BulkAssignRescuersToAssemblyPointCommand(dto.UserIds, dto.AssemblyPointId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Lên lịch tập trung tại điểm tập kết → tạo AssemblyEvent + gán participant + gửi Firebase.</summary>
        [HttpPost("{id}/schedule-gathering")]
        [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)]
        public async Task<IActionResult> ScheduleGathering(int id, [FromBody] ScheduleGatheringRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var createdBy))
                return Unauthorized();

            var command = new ScheduleGatheringCommand(id, dto.AssemblyDate, dto.CheckInDeadline, createdBy);
            var eventId = await _mediator.Send(command);
            return Ok(new { EventId = eventId });
        }

        /// <summary>Rescuer check-in tại sự kiện tập trung (kèm GPS validation trong bán kính 200m).</summary>
        [HttpPost("events/{eventId}/check-in")]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyEventCheckIn)]
        public async Task<IActionResult> CheckIn(int eventId, [FromBody] CheckInRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var command = new CheckInAtAssemblyPointCommand(eventId, userId, dto.Latitude, dto.Longitude);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Rescuer check-out khỏi sự kiện tập trung.</summary>
        [HttpPost("events/{eventId}/check-out")]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyEventCheckIn)]
        public async Task<IActionResult> CheckOut(int eventId, [FromBody] CheckInRequestDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var command = new CheckOutAtAssemblyPointCommand(eventId, userId, dto.Latitude, dto.Longitude);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Đội trưởng đánh dấu một thành viên vắng mặt tại sự kiện tập trung.
        /// Ghi nhận trạng thái Absent và thông báo tới coordinator để bổ sung thành viên.
        /// </summary>
        [HttpPost("events/{eventId}/participants/{rescuerId}/mark-absent")]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyEventCheckIn)]
        public async Task<IActionResult> MarkParticipantAbsent(int eventId, Guid rescuerId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var callerUserId))
                return Unauthorized();

            var command = new MarkParticipantAbsentCommand(eventId, rescuerId, callerUserId);
            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>Lấy danh sách rescuer đã check-in tại sự kiện tập trung (để coordinator chia team).</summary>
        /// <summary><paramref name="search"/>: tìm kiếm theo firstName, lastName, phone hoặc email (OR).</summary>
        [HttpGet("events/{eventId}/checked-in-rescuers")]
        [Authorize(Policy = PermissionConstants.PolicyPersonnelManage)]
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
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyEventSelfView)]
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

        /// <summary>
        /// Rescuer xem danh sách sự kiện tập trung sắp tới đang ở trạng thái Gathering
        /// mà mình được gán vào. Kết quả sắp xếp theo thời gian triệu tập tăng dần (gần nhất trước).
        /// </summary>
        [HttpGet("events/my/upcoming")]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyEventSelfView)]
        public async Task<IActionResult> GetMyUpcomingAssemblyEvents()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var query = new GetMyUpcomingAssemblyEventsQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        // ──────────────────────────────────────────────────────────
        // Per-assembly-point check-in radius config endpoints
        // ──────────────────────────────────────────────────────────

        /// <summary>Lấy toàn bộ cấu hình bán kính check-in riêng đang được thiết lập theo từng điểm tập kết (chỉ trả về các điểm đã có cấu hình riêng).</summary>
        [HttpGet("check-in-radius")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> GetAllCheckInRadiusConfigs()
        {
            var query = new GetAllAssemblyPointCheckInRadiusConfigsQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Lấy cấu hình bán kính check-in của điểm tập kết. Trả về cấu hình riêng nếu có, ngược lại trả về cấu hình toàn cục.</summary>
        [HttpGet("{id}/check-in-radius")]
        [Authorize(Policy = PermissionConstants.PersonnelAssemblyPointView)]
        public async Task<IActionResult> GetCheckInRadius(int id)
        {
            var query = new GetAssemblyPointCheckInRadiusQuery(id);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Thiết lập hoặc cập nhật bán kính check-in riêng cho điểm tập kết.</summary>
        [HttpPut("{id}/check-in-radius")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> UpsertCheckInRadius(int id, [FromBody] UpsertAssemblyPointCheckInRadiusRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var command = new UpsertAssemblyPointCheckInRadiusCommand(id, request.MaxRadiusMeters, userId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xóa cấu hình bán kính check-in riêng của điểm tập kết; điểm sẽ quay về dùng cấu hình toàn cục.</summary>
        [HttpDelete("{id}/check-in-radius")]
        [Authorize(Policy = PermissionConstants.PersonnelGlobalManage)]
        public async Task<IActionResult> DeleteCheckInRadius(int id)
        {
            var command = new DeleteAssemblyPointCheckInRadiusCommand(id);
            await _mediator.Send(command);
            return NoContent();
        }
    }
}


