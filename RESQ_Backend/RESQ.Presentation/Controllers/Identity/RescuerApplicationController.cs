using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.AddRescuerDocuments;
using RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;
using RESQ.Application.UseCases.Identity.Queries.GetMyRescuerApplication;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/user/rescuer")]
    [ApiController]
    [Authorize]
    public class RescuerApplicationController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>Nộp đơn đăng ký làm rescuer kèm thông tin và tài liệu (URLs đã upload).</summary>
        [HttpPost("apply")]
        public async Task<IActionResult> SubmitRescuerApplication([FromBody] SubmitRescuerApplicationRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new SubmitRescuerApplicationCommand(
                userId,
                dto.RescuerType,
                dto.FirstName,
                dto.LastName,
                dto.Phone,
                dto.Address,
                dto.Ward,
                dto.Province,
                dto.Latitude,
                dto.Longitude,
                dto.Note
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Xem đơn đăng ký rescuer của mình (đơn mới nhất).</summary>
        [HttpGet("application")]
        [Authorize(Roles = "3")] // Rescuer only
        public async Task<IActionResult> GetMyRescuerApplication()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var query = new GetMyRescuerApplicationQuery(userId);
            var result = await _mediator.Send(query);

            if (result is null)
            {
                return NotFound(new { Message = "Bạn chưa có đơn đăng ký nào" });
            }

            return Ok(result);
        }

        /// <summary>Thêm tài liệu vào đơn đăng ký rescuer (bổ sung vào danh sách hiện tại).</summary>
        [HttpPost("documents")]
        public async Task<IActionResult> AddRescuerDocuments([FromBody] AddRescuerDocumentsRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new AddRescuerDocumentsCommand(userId, dto.Documents);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>Thay thế toàn bộ tài liệu của đơn đăng ký rescuer (xoá cũ, thêm mới).</summary>
        [HttpPut("documents")]
        public async Task<IActionResult> ReplaceRescuerDocuments([FromBody] ReplaceRescuerDocumentsRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new ReplaceRescuerDocumentsCommand(userId, dto.Documents);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
