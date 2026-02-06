using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("api/user")]
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
    }
}
