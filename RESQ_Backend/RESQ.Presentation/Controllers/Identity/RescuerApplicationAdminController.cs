using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Constants;
using RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/admin/rescuer-applications")]
    [ApiController]
    [Authorize]
    public class RescuerApplicationAdminController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Lấy danh sách đơn đăng ký rescuer có phân trang (lọc theo status, tên, email, phone).</summary>
        [HttpGet]
        [Authorize(Policy = PermissionConstants.SystemUserView)]
        public async Task<IActionResult> GetRescuerApplications(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? name = null,
            [FromQuery] string? email = null,
            [FromQuery] string? phone = null,
            [FromQuery] string? rescuerType = null)
        {
            var query = new GetRescuerApplicationsQuery(pageNumber, pageSize, status, name, email, phone, rescuerType);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>Duyệt hoặc từ chối đơn đăng ký rescuer.</summary>
        [HttpPost("review")]
        [Authorize(Policy = PermissionConstants.SystemUserManage)]
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
