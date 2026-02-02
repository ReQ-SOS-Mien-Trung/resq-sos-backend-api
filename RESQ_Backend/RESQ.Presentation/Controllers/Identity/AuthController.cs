using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.UseCases.Identity.Commands.GoogleLogin;
using RESQ.Application.UseCases.Identity.Commands.Login;
using RESQ.Application.UseCases.Identity.Commands.Logout;
using RESQ.Application.UseCases.Identity.Commands.RefreshToken;
using RESQ.Application.UseCases.Identity.Commands.Register;
using RESQ.Application.UseCases.Identity.Commands.RegisterRescuer;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Commands.VerifyEmail;
using RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail;

namespace RESQ.Presentation.Controllers.Identity
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            var command = new RegisterCommand(dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("register-rescuer")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterRescuer([FromBody] RegisterRescuerRequestDto dto)
        {
            var command = new RegisterRescuerCommand(dto.Email, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var command = new VerifyEmailCommand(token);
            var result = await _mediator.Send(command);
            if (result.Success)
            {
                return Redirect("http://localhost:5173/auth/personal-info");
            }
            return BadRequest(result);
        }

        [HttpPost("resend-verification-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequestDto dto)
        {
            var command = new ResendVerificationEmailCommand(dto.Email);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var command = new LoginCommand(dto.Username, dto.Phone, dto.Password);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
        {
            var command = new GoogleLoginCommand(dto.IdToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var command = new RefreshTokenCommand(dto.AccessToken, dto.RefreshToken);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var command = new LogoutCommand(userId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("rescuer-consent")]
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

        [HttpPut("rescuer-profile")]
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
                dto.District,
                dto.City,
                dto.Latitude,
                dto.Longitude
            );
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
