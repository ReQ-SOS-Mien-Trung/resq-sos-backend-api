using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/admin/rescuer-applications")]
    [ApiController]
    [Authorize(Roles = "1")] // Admin only
    public class RescuerApplicationAdminController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>
        /// Lấy danh sách đơn đăng ký rescuer (có phân trang)
        /// </summary>
        /// <param name="pageNumber">Số trang (mặc định: 1)</param>
        /// <param name="pageSize">Số lượng mỗi trang (mặc định: 10)</param>
        /// <param name="status">Lọc theo trạng thái: Pending, Approved, Rejected (để trống = tất cả)</param>
        [HttpGet]
        public async Task<IActionResult> GetRescuerApplications(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var query = new GetRescuerApplicationsQuery(pageNumber, pageSize, status);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Duyệt hoặc từ chối đơn đăng ký rescuer
        /// </summary>
        /// <param name="dto">Thông tin duyệt đơn</param>
        [HttpPost("review")]
        public async Task<IActionResult> ReviewRescuerApplication([FromBody] ReviewRescuerApplicationRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var adminUserId))
            {
                return Unauthorized();
            }

            var command = new ReviewRescuerApplicationCommand(
                dto.ApplicationId,
                adminUserId,
                dto.IsApproved,
                dto.AdminNote
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
