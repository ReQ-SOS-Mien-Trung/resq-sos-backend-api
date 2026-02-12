using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;
using RESQ.Application.UseCases.Identity.Queries.GetMyRescuerApplication;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("identity/user")]
    [ApiController]
    public class UserController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var query = new GetCurrentUserQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("rescuer/consent")]
        [Authorize]
        public async Task<IActionResult> RescuerConsent([FromBody] RescuerConsentRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new RescuerConsentCommand(
                userId,
                dto.AgreeMedicalFitness,
                dto.AgreeLegalResponsibility,
                dto.AgreeTraining,
                dto.AgreeCodeOfConduct
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPut("rescuer/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateRescuerProfile([FromBody] UpdateRescuerProfileRequestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new UpdateRescuerProfileCommand(
                userId,
                dto.FirstName,
                dto.LastName,
                dto.Phone,
                dto.Address,
                dto.Ward,
                dto.City,
                dto.Latitude,
                dto.Longitude
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Submit đơn đăng ký làm rescuer với thông tin và tài liệu chứng minh (URLs)
        /// </summary>
        /// <param name="dto">Thông tin đơn đăng ký (documents là URLs đã upload lên cloud)</param>
        /// <returns>Kết quả submit đơn đăng ký</returns>
        [HttpPost("rescuer/apply")]
        [Authorize]
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
                dto.FullName,
                dto.Phone,
                dto.Address,
                dto.Ward,
                dto.City,
                dto.Note,
                dto.Documents
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        /// <summary>
        /// Xem đơn đăng ký rescuer của mình (đơn mới nhất)
        /// </summary>
        /// <returns>Thông tin đơn đăng ký</returns>
        [HttpGet("rescuer/application")]
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
    }
}
